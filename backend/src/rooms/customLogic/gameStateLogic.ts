import { ArenaRoom, ClientReadyState } from "../ArenaRoom";
import  ServerGameState  from "./enums/ServerGameState";
import CountDownState from "./enums/CountDownState";
import GameState from "./enums/GameState";
import { roomOptions } from "../ArenaRoomController";

import { checkIfUsersReady, getRoundWinner, resetForNewRound } from "./gameLogic";


const logger = require("../../helpers/logger");

const GeneralMessage = "generalMessage";
const BeginRoundCountDown = "countDown";
const CountDownTime = 3;

let moveToState = function (roomRef: ArenaRoom, newState: string) {
    roomRef.setRoomAttribute( GameState.Last, roomRef.getGameState( GameState.Current));
    roomRef.setRoomAttribute( GameState.Current, newState);
}

let gameLoop = function (roomRef: ArenaRoom, deltaTime: number){
    switch(roomRef.getGameState(GameState.Current)){
        case ServerGameState.None:
            break;
        case ServerGameState.Waiting:
            waitingLogic(roomRef, deltaTime);
            break;
        case ServerGameState.BeginRound:
            beginRoundLogic(roomRef, deltaTime);
            break;
        case ServerGameState.SimulateRound:
            simulateRoundLogic(roomRef, deltaTime);
            break;
        case ServerGameState.EndRound:
            endRoundLogic(roomRef, deltaTime);
            break;
        default:
            logger.error(`Unknown Game State - ${roomRef.getGameState( GameState.Current)}`);
            break;
    }
}

let waitingLogic = function (roomRef: ArenaRoom, deltaTime: number) {
    let playersReady = false;
    switch(roomRef.getGameState( GameState.Last)){
        case ServerGameState.None:
        case ServerGameState.EndRound:
            const currentUsers = roomRef.state.networkedUsers.size;
            let minReqPlayersToStartRound = Number(roomOptions["minReqPlayers"] || 2);
            if(currentUsers < minReqPlayersToStartRound) {
                roomRef.state.attributes.set(GeneralMessage, `Waiting for more players to join - (${currentUsers}/${minReqPlayersToStartRound})`);
                return;
            }
            playersReady = checkIfUsersReady(roomRef.state.networkedUsers);
            if(playersReady == false) return;
           roomRef.lock();
           roomRef.state.networkedEntities.forEach((value, key)=>{
               roomRef.currentAliveSet.set(value.id, "alive");
           })
           moveToState(roomRef, ServerGameState.BeginRound);
            break;
    }
}

let beginRoundLogic = function (roomRef: ArenaRoom, deltaTime: number) {
    let roomState = roomRef.state.attributes.get("CurrentCountDownState");
    if(roomState == null) {
        roomState = CountDownState.Enter;
        roomRef.state.attributes.set("CurrentCountDownState",roomState);
    }

    switch (roomState) {
        case CountDownState.Enter:
            roomRef.setRoomAttribute( BeginRoundCountDown, "");
            roomRef.broadcast("beginRoundCountDown", {});
            roomRef.state.attributes.set("currCountDown","0");
            roomState = CountDownState.GetReady;
            break;
        case CountDownState.GetReady:
            roomRef.setRoomAttribute( BeginRoundCountDown, "Get Ready!");
            var currCountDown = Number(roomRef.state.attributes.get("currCountDown"));
            if(currCountDown < 3){
                currCountDown += deltaTime;
                roomRef.state.attributes.set("currCountDown",currCountDown.toString());
                return;
            }
            roomState = CountDownState.CountDown;
            roomRef.state.attributes.set("currCountDown",CountDownTime.toString());
            break;
        case CountDownState.CountDown:
            var currCountDown = Number(roomRef.state.attributes.get("currCountDown"));
            roomRef.setRoomAttribute( BeginRoundCountDown, Math.ceil(currCountDown).toString());

            if (currCountDown >= 0) {
                currCountDown -= deltaTime;
                roomRef.state.attributes.set("currCountDown",currCountDown.toString());
                return;
            }
            roomRef.broadcast("beginRound", {});
            moveToState(roomRef, ServerGameState.SimulateRound);
            
            roomRef.state.networkedUsers.forEach((value, key)=>{
                roomRef.gameScores.set(value.id, 0);
            })
            roomRef.setAttributeForAllUser( ClientReadyState, "waiting");
            roomState = CountDownState.Enter;
            break;
    }
    roomRef.state.attributes.set("CurrentCountDownState",roomState);    
}


let simulateRoundLogic = function (roomRef: ArenaRoom, deltaTime: number) {
    if (roomRef.currentAliveSet.size > 1) {
        return;
    }
    moveToState(roomRef, ServerGameState.EndRound);
}


let endRoundLogic = function (roomRef: ArenaRoom, deltaTime: number) {
    const winner = getRoundWinner(roomRef);
    roomRef.broadcast("onRoundEnd", { winner });
    resetForNewRound(roomRef);
    moveToState(roomRef, ServerGameState.Waiting);
}

export {
    gameLoop,
};