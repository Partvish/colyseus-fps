import { Room, Client, generateId } from "colyseus";
import { ArenaRoomController } from "./ArenaRoomController";
import { ColyseusRoomState, ColyseusNetworkedEntity, ColyseusNetworkedUser } from "./schema/ColyseusRoomState";

const logger = require("../helpers/logger");

const ClientReadyState = "readyState";

class ArenaRoom extends Room<ColyseusRoomState> {
    clientEntities = new Map<string, string[]>();
    serverTime: number = 0;
    roomOptions: any;
    roomController: ArenaRoomController = new ArenaRoomController();
    gameScores: Map<string, number>;
    currentAliveSet: Map<string, any>;
        
    async onCreate(options: any) {
        logger.info("--------------------------------------------- Room Created");
        console.log(options);
        logger.info("---------------------------------------------");

        this.maxClients = 25;
        this.roomOptions = options;

        if(options["roomId"] != null) {
            this.roomId = options["roomId"];           
        }

        this.setState(new ColyseusRoomState());

        this.onMessage("ping", (client) => {
            client.send(0, { serverTime: this.serverTime });
        });

        this.onMessage("customMethod", (client, request) => {
            try {
                this.roomController.ProcessMethod(this, client, request);
            } catch (error) {
                logger.error("Error with custom Method logic: " + error);
            }
        });

        this.onMessage("entityUpdate", (client, entityUpdateArray) => {
            if(this.state.networkedEntities.has(`${entityUpdateArray[0]}`) === false) return;
            if(this.state.networkedEntities.has(`${entityUpdateArray[0]}`) === false) return;

            let stateToUpdate = this.state.networkedEntities.get(entityUpdateArray[0]);
            
            let startIndex = 1;
            if(entityUpdateArray[1] === "attributes") startIndex = 2;
            
            for (let i = startIndex; i < entityUpdateArray.length; i+=2) {
                const property = entityUpdateArray[i];
                let updateValue = entityUpdateArray[i+1];
                if(updateValue === "inc") {
                    updateValue = entityUpdateArray[i+2];
                    updateValue = parseFloat(stateToUpdate.attributes.get(property)) +  parseFloat(updateValue);
                    i++; 
                }
    
                if(startIndex == 2) {
                    stateToUpdate.attributes.set(property, updateValue.toString());
                } else {
                    (stateToUpdate as any)[property] = updateValue;
                }
            }
    
            stateToUpdate.timestamp = parseFloat(this.serverTime.toString());
        });

        this.onMessage("remoteFunctionCall", (client, RFCMessage) => {
            if(this.state.networkedEntities.has(`${RFCMessage.entityId}`) === false) return;
            RFCMessage.clientId = client.id;
            this.broadcast("onRFC", RFCMessage, RFCMessage.target == 0 ? {} : {except : client});
        });

        this.onMessage("setAttribute", (client, attributeUpdateMessage) => {
            this.setAttribute(client, attributeUpdateMessage); 
        });

        this.onMessage("removeEntity", (client, removeId) => {
            if(this.state.networkedEntities.has(removeId)) {
                this.state.networkedEntities.delete(removeId);
            }
        });

        this.onMessage("createEntity", (client, creationMessage) => {
            let entityViewID = generateId();
            let newEntity = new ColyseusNetworkedEntity().assign({
                id: entityViewID,
                ownerId: client.id,
                timestamp: this.serverTime
            });

            if(creationMessage.creationId != null) newEntity.creationId = creationMessage.creationId;

            newEntity.timestamp = parseFloat(this.serverTime.toString());

            for (let key in creationMessage.attributes) {
                if(key === "creationPos")
                {
                    newEntity.xPos = parseFloat(creationMessage.attributes[key][0]);
                    newEntity.yPos = parseFloat(creationMessage.attributes[key][1]);
                    newEntity.zPos = parseFloat(creationMessage.attributes[key][2]);
                }
                else if(key === "creationRot")
                {
                    newEntity.xRot = parseFloat(creationMessage.attributes[key][0]);
                    newEntity.yRot = parseFloat(creationMessage.attributes[key][1]);
                    newEntity.zRot = parseFloat(creationMessage.attributes[key][2]);
                    newEntity.wRot = parseFloat(creationMessage.attributes[key][3]);
                }
                else {
                    newEntity.attributes.set(key, creationMessage.attributes[key].toString());
                }
            }

            this.state.networkedEntities.set(entityViewID, newEntity);

            if(this.clientEntities.has(client.id)) {
                this.clientEntities.get(client.id).push(entityViewID);
            } else {
                this.clientEntities.set(client.id, [entityViewID]);
            }
            
        });
        this.setPatchRate(1000 / 20);
        this.roomController.InitializeLogic(this, options);

        this.setSimulationInterval(dt => {
            this.serverTime += dt;
            try {
                this.roomController.ProcessLogic(this, dt);
            } catch (error) {
                logger.error("Error with custom room logic: " + error);
            }
        } );

    }


    onJoin(client: Client, options: any) {
        logger.info(`Client joined: ${client.sessionId}`);
       
        let newNetworkedUser = new ColyseusNetworkedUser().assign({
            id: client.id,
            sessionId: client.sessionId,
        });
        
        this.state.networkedUsers.set(client.sessionId, newNetworkedUser);

        client.send("onJoin", newNetworkedUser);
    }

    setAttribute (client: Client, attributeUpdateMessage: any) {
        if(attributeUpdateMessage == null 
            || (attributeUpdateMessage.entityId == null && attributeUpdateMessage.userId == null)
            || attributeUpdateMessage.attributesToSet == null
                ) {
            return; 
        }

        if(attributeUpdateMessage.entityId){
            if(this.state.networkedEntities.has(`${attributeUpdateMessage.entityId}`) === false) return;
            
            this.state.networkedEntities.get(`${attributeUpdateMessage.entityId}`).timestamp = parseFloat(this.serverTime.toString());
            let entityAttributes = this.state.networkedEntities.get(`${attributeUpdateMessage.entityId}`).attributes;
            for (let index = 0; index < Object.keys(attributeUpdateMessage.attributesToSet).length; index++) {
                let key = Object.keys(attributeUpdateMessage.attributesToSet)[index];
                let value = attributeUpdateMessage.attributesToSet[key];
                entityAttributes.set(key, value);
            }
        }
        else if(attributeUpdateMessage.userId) {
            
            if(this.state.networkedUsers.has(`${attributeUpdateMessage.userId}`) === false) {
                logger.error(`Set Attribute - User Attribute - Room does not have user with Id - \"${attributeUpdateMessage.userId}\"`);
                return;
            }

            this.state.networkedUsers.get(`${attributeUpdateMessage.userId}`).timestamp = parseFloat(this.serverTime.toString());

            let userAttributes = this.state.networkedUsers.get(`${attributeUpdateMessage.userId}`).attributes;

            for (let index = 0; index < Object.keys(attributeUpdateMessage.attributesToSet).length; index++) {
                let key = Object.keys(attributeUpdateMessage.attributesToSet)[index];
                let value = attributeUpdateMessage.attributesToSet[key];
                userAttributes.set(key, value);
            }
        }

    }

    async onLeave(client: Client, consented: boolean) {
        let networkedUser = this.state.networkedUsers.get(client.sessionId);
        if(networkedUser){
            networkedUser.connected = false;
        }
        logger.silly(`!!! User Leave - ${client.sessionId}`);

        try {
            if (consented) {
                throw new Error("consented leave!");
            }

            logger.info("let's wait for reconnection for client: " + client.sessionId);
            const newClient = await this.allowReconnection(client, 10);
            logger.info("reconnected! client: " + newClient.id);

        } catch (e) {
            logger.info("disconnected! client: " + client.id);
            logger.silly(`!!!Removing Networked User and Entity ${client.id}`);

            this.state.networkedUsers.delete(client.sessionId);
            if(this.clientEntities.has(client.id)) {
                let allClientEntities = this.clientEntities.get(client.id);
                allClientEntities.forEach(element => {
                    this.state.networkedEntities.delete(element);
                });

                this.clientEntities.delete(client.id);
                this.roomController.ProcessUserLeft(this);
            } 
            else{
                logger.error(`Can't remove entities for ${client.id} - No entry in Client Entities!`);
            }
        }
    }

    onDispose() {
    }

    getGameState(gameState: string){
        return this.state.attributes.get(gameState);
    }

    setRoomAttribute(key: string, value: string){
        this.state.attributes.set(key, value);
    }

    setAttributeForAllUser( key: string, value: string) {
        this.state.networkedUsers.forEach((userValue, userKey) => {
            let msg: any = {userId: userKey, attributesToSet: {}};
            msg.attributesToSet[key] = value;
            this.setAttribute(null, msg);
        });
    }
    unlockIfAble() {
        if(this.hasReachedMaxClients() === false) {
            this.unlock();
        }
    }
}

export {
    ArenaRoom,
    ClientReadyState
};