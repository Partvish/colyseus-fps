import { Client } from "colyseus";
import { ArenaRoom } from "./ArenaRoom";
import { customMethods } from "./customLogic/clientRequest";
import  GameState  from "./customLogic/enums/GameState";
import ServerGameState from "./customLogic/enums/ServerGameState";
import { gameLoop } from "./customLogic/gameStateLogic";

const logger = require("../helpers/logger");

let roomOptions: any;

class ArenaRoomController {

InitializeLogic(roomRef: ArenaRoom, options: any) {
    roomOptions = options;
    roomRef.currentAliveSet = new Map();
    roomRef.gameScores = new Map();

    roomRef.setRoomAttribute( GameState.Current, ServerGameState.Waiting)
    roomRef.setRoomAttribute( GameState.Last, ServerGameState.None);
}

ProcessLogic(roomRef: ArenaRoom, deltaTime: number) {
    gameLoop(roomRef, deltaTime/1000); 
}

ProcessMethod (roomRef: ArenaRoom, client: Client, request: any) {
    
    if (request.method in customMethods && typeof customMethods[request.method] === "function") {
        customMethods[request.method](roomRef, client, request);
    } else {
        throw "No Method: " + request.method + " found";
        return; 
    }
}

ProcessUserLeft (roomRef: ArenaRoom) {
    if(roomRef.locked)
    {
        switch(roomRef.getGameState( GameState.Current)){
        case ServerGameState.Waiting:
            roomRef.unlockIfAble();
            break;
        case ServerGameState.BeginRound:
        case ServerGameState.SimulateRound:
        case ServerGameState.EndRound:
            logger.silly(`Will not unlock the room, Game State - ${roomRef.getGameState( GameState.Current)}`);
            break;
        }
    }
 }
}

export {
    roomOptions,
    ArenaRoomController
};