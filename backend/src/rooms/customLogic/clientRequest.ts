import { Client } from "colyseus";

import { ArenaRoom } from "../ArenaRoom";
import { scoreTargetForEntity } from "./gameLogic";


const logger = require("../../helpers/logger");

const customMethods: any = {};

customMethods.hitPlayer = function (roomRef: ArenaRoom, client: Client, request: any) {
    const param = request.param;

    const entityID = param[0];
    const targetID = param[1];

    logger.silly("shot from: " + entityID + " to " + targetID);

    if(roomRef.state.networkedEntities.has(entityID)){
        if (roomRef.state.networkedEntities.has(targetID)) {
            if(roomRef.currentAliveSet.has(targetID)){
                scoreTargetForEntity(roomRef, entityID, targetID);
            }
            else{
                logger.silly(`Target is already dead! - ${targetID}`);
            }
        }
        else {
            logger.silly(`No Target Entity with ID: ${entityID}`);
        }
    }
    else{
        logger.error(`No Entity with ID: ${entityID}`)
    }
}

export {
    customMethods,
}