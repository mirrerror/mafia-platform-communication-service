# Mafia Platform Communication Service

## Service Responsibilities

Facilitates all in-game chat. It provides a global chat during the voting phase and private, secure chat channels for specific groups (e.g., Mafia members, players in the same location).

## Technology Stack
С# (ASP .NET Core, SignalR), PostgreSQL as the database, Websockets for the communcation.

## API Endpoints

All request and response bodies are in **JSON** format.

**Technology:** WebSockets

### WS `/api/app/chat/global/{lobbyId}`

Used for sending messages to the global chat in the specified lobby.

### WS `/api/topic/chat/global/{lobbyId}`

Used for subscription to the incoming global chat messages in the specified lobby.

### WS `/api/app/chat/private/{channelName}/{lobbyId}`

Used for sending messages to a private chat with the specified name in the specified lobby.

### WS `/api/topic/chat/private/{channelName}/{lobbyId}`

Used for subscription to the incoming messages from a private chat with the specified name in the specified lobby.

**Message Format (Client → Server):**

```json
{ "content": "string" }
```

**Message Format (Server → Client):**

```json
{ "sender": "string", "content": "string", "timestamp": "datetime" }
```
