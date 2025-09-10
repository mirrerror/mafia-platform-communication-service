# Mafia Platform Communication Service

## Service Responsibilities

Facilitates all in-game chat. It provides a global chat during the voting phase and private, secure chat channels for specific groups (e.g., Mafia members, players in the same location).

## Technology Stack
С# (ASP .NET Core, SignalR), PostgreSQL as the database, Websockets for the communcation.

## API Reference

All request and response bodies are in **JSON** format.

### Send Global Message

**Endpoint:** `POST /api/chat/global/{lobbyId}/send-message`

**Description:** Sends a message to the specified global chat in the specified lobby.

**URL Parameters:**

* `lobbyId` *(string)* – Unique lobby identifier.

**Request Body:** [ChatMessage Model](#chatmessage-model)

**Success Response (200):**
```json
{
  "senderId": 123,
  "senderName": "PlayerOne",
  "content": "Hello everyone!",
  "timestamp": "2025-09-10T14:30:45.123Z"
}
```

**Error Responses:**

**400 Bad Request - Validation Error**
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Content must not exceed 200 characters"
  }
}
```

**400 Bad Request - Chat Disabled**
```json
{
  "error": {
    "code": "CHAT_DISABLED",
    "message": "Global chat is currently disabled for this lobby"
  }
}
```

**401 Unauthorized**
```json
{
  "error": {
    "code": "INVALID_TOKEN",
    "message": "Invalid or expired token"
  }
}
```

**404 Not Found**
```json
{
  "error": {
    "code": "LOBBY_NOT_FOUND",
    "message": "Lobby does not exist"
  }
}
```

---

### Send Private Message

**Endpoint:** `POST /api/chat/private/{lobbyId}/{channelName}/send-message`

**Description:** Sends a message to all clients in the specified private channel inside the specified lobby.

**URL Parameters:**

* `lobbyId` *(string)* – The lobby identifier.
* `channelName` *(string)* – The private channel name.

**Request Body:** [ChatMessage Model](#chatmessage-model)

**Success Response (200):**
```json
{
  "senderId": 456,
  "senderName": "MafiaPlayer",
  "content": "We should eliminate PlayerOne tonight",
  "timestamp": "2025-09-10T14:32:15.789Z"
}
```

**Error Responses:**

**400 Bad Request - Validation Error**
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Sender name must be between 2 and 50 characters"
  }
}
```

**401 Unauthorized**
```json
{
  "error": {
    "code": "INVALID_TOKEN",
    "message": "Invalid or expired token"
  }
}
```

**403 Forbidden**
```json
{
  "error": {
    "code": "ACCESS_DENIED",
    "message": "You do not have access to this private channel"
  }
}
```

**404 Not Found - Lobby**
```json
{
  "error": {
    "code": "LOBBY_NOT_FOUND",
    "message": "Lobby does not exist"
  }
}
```

**404 Not Found - Channel**
```json
{
  "error": {
    "code": "CHANNEL_NOT_FOUND",
    "message": "Private channel does not exist"
  }
}
```

---

### Toggle Global Chat

**Endpoint:** `POST /api/chat/global/{lobbyId}/toggle`

**Description:** Enables/disables (toggles) the global chat in the specified lobby.

**Success Response (200) - Chat Enabled:**
```json
{
  "lobbyId": "550e8400-e29b-41d4-a716-446655440000",
  "isGlobalChatEnabled": true
}
```

**Success Response (200) - Chat Disabled:**
```json
{
  "lobbyId": "550e8400-e29b-41d4-a716-446655440000",
  "isGlobalChatEnabled": false
}
```

**Error Responses:**

**404 Not Found**
```json
{
  "error": {
    "code": "LOBBY_NOT_FOUND",
    "message": "Lobby does not exist"
  }
}
```

---

## SignalR Hub Reference

### Server Methods (Client → Server)

| Method                                                                        | Description                                                                            |
| ----------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `JoinGlobalChat(string lobbyId, long userId)`                                             | Adds the specified user to the global chat in the specified lobby.                                                     |
| `LeaveGlobalChat(string lobbyId, long userId)`                                            | Removes the specified user from the global chat in the specified lobby.                                                |
| `JoinPrivateChannel(string lobbyId, string channelName, long userId)`                      | Adds the specified user to the specified private channel.                                                  |
| `LeavePrivateChannel(string lobbyId, string channelName, long userId)`                     | Removes the speicified user from the specified private channel.                                             |
| `SendGlobalMessage(string lobbyId, ChatMessage message)`                      | Broadcasts a message to the global chat in the specified lobby. Throws `HubException` on validation errors.    |
| `SendPrivateMessage(string channelName, string lobbyId, ChatMessage message)` | Broadcasts a message to the specified private channel in the specified lobby. Throws `HubException` on validation errors. |

---

### Client Methods (Server → Client)

| Method                                         | Description                                                |
| ---------------------------------------------- | ---------------------------------------------------------- |
| `ReceiveGlobalMessage(ChatResponse response)`  | Triggered when a new message arrives in a global chat.    |
| `ReceivePrivateMessage(ChatResponse response)` | Triggered when a new message arrives in a private channel. |

---

## Data Models

### ChatMessage Model

| Field       | Type   | Description                          | Validation Rules |
|-------------|--------|--------------------------------------|------------------|
| `senderId`  | long   | The unique ID of the sender         | Required, must be ≥ 0 |
| `senderName`| string | The display name of the sender      | Required, 2–50 characters |
| `content`   | string | The text content of the message     | Required, not empty, max 200 characters |

**Example:**

```json
{
  "senderId": 123,
  "senderName": "TestUser",
  "content": "Hello, world!"
}
```

---

### ChatResponse Model

| Field       | Type     | Description |
|-------------|----------|-------------|
| `senderId`  | long     | ID of the sender |
| `senderName`| string   | Name of the sender |
| `content`   | string   | Message content |
| `timestamp` | DateTime | UTC timestamp from server |

**Example:**
```json
{
  "senderId": 123,
  "senderName": "TestUser",
  "content": "Hello, world!",
  "timestamp": "2025-09-09T20:30:00.123Z"
}
```
