import { ColyseusRoomState } from "../schema/ColyseusRoomState";
import { ArenaRoom, ClientReadyState } from "../ArenaRoom";
import GameState from "./enums/GameState"
import ServerGameState from "./enums/ServerGameState";

const logger = require("../../helpers/logger");


let scoreTargetForEntity = function(roomRef: ArenaRoom, entityID: string, targetID: string) {
    roomRef.currentAliveSet.delete(targetID);
    if (roomRef.gameScores.has(entityID)) {
        roomRef.gameScores.set(entityID, roomRef.gameScores.get(entityID) +1);
    }
    else {
        roomRef.gameScores.set(entityID, 1);
    }
    roomRef.broadcast("onScoreUpdate", { entityID, targetID, score: roomRef.gameScores.get(entityID) });
}

let checkIfUsersReady = function(users: ColyseusRoomState['networkedUsers']) {
    let everyoneReady = true;
    Array.from(users.entries()).forEach( entry =>  {
        let readyState = entry[1].attributes.get(ClientReadyState);
        if(readyState == null || readyState != "ready"){
            everyoneReady = false;
        }
    })
    return everyoneReady;
}


let getRoundWinner = function (roomRef: ArenaRoom) {
    const winner: any = { id: "", score: 0, tie: false, tied: [] };
    if(roomRef.getGameState( GameState.Current) != ServerGameState.EndRound){
        logger.error("Can't determine winner yet! Not in End Round");
        winner.id = "TBD";
        return winner;
    }
    let scoreMap: any = {};
    try {
        roomRef.gameScores.forEach((score, player) => {
            if(!roomRef.state.networkedEntities.has(player)) {
                return;
            }
            if(scoreMap[score] == null)
                scoreMap[score] = [player];
            else
                scoreMap[score].push(player);
            if(score > winner.score){
                winner.id = player;
                winner.score = score;
            }
        });
        if(scoreMap == undefined || winner.score == undefined || scoreMap[winner.score] == undefined) {
            logger.error("Failed to get scoreMap or winner");
        }
        if(scoreMap[winner.score].length > 1) {
            winner.id = "It's a tie!";
            winner.tie = true;
            winner.tied = scoreMap[winner.score];
        }
    } catch(error) {
        logger.error("Failed in getRoundWinner");
        logger.error(error);
    }
    return winner;
}

let resetForNewRound = function (roomRef: ArenaRoom) {
    //roomRef.gameScores.clear();
    roomRef.currentAliveSet.clear();
    roomRef.setAttributeForAllUser( ClientReadyState, "waiting");
    roomRef.unlockIfAble();
}

export {
    checkIfUsersReady,
    getRoundWinner,
    resetForNewRound,
    scoreTargetForEntity
};