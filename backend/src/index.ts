import express from "express";
import { Server } from "colyseus";
import { monitor } from "@colyseus/monitor";
import http from 'http';
import { ArenaRoom } from "./rooms/ArenaRoom";

const port = 2567;
const app = express()
app.use(express.json())

const server = http.createServer(app);
const gameServer = new Server({server,});
app.use("/colyseus", monitor());

gameServer.listen(port);

console.log(`GameServer is up! Listening on //localhost:${ port }`)
