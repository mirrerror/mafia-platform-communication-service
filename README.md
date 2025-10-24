# Mafia Platform Communication Service

## Service Responsibilities

Facilitates all in-game chat. It provides a global chat during the voting phase and private, secure chat channels for specific groups (e.g., Mafia members, players in the same location).

---

## Technology Stack
С# (ASP .NET Core, SignalR), PostgreSQL as the database, Websockets for the communcation.

---

## Prerequisites
Before you begin, ensure you have the following installed on your machine:

- Git
- .NET 9.0 SDK
- Docker Desktop

---

## Getting Started

### 1. Clone the Repository
Clone the project to your local machine:

```bash
git clone https://github.com/mirrerror/mafia-platform-communication-service.git
cd mafia-platform-communication-service
```

---

### 2. Configure Environment Variables

This project uses a `.env` file in the root directory to manage sensitive information like database connection strings.

1. Find the `.env.example` file in the root of the project, make a copy, and rename it to `.env`.
2. Open `.env` and configure it.

**Important Note on Host:**

* When running with Docker Compose → `Host=db`
* When running locally → `Host=localhost`

---

## How to Run the Service

### Option 1: Using Docker Compose (Recommended)

This method runs both the .NET service and PostgreSQL inside containers.

1. Ensure Docker Desktop is running.
2. Update `.env` so the connection string uses `Host=db`:

```env
ConnectionStrings__DefaultConnection="Host=db;Database=mafia_chat;..."
```

3. Build and run:

```bash
docker-compose up --build
```

The service will be available at [http://localhost:8080](http://localhost:8080).

* Stop: press **Ctrl + C**
* Stop & remove containers:

```bash
docker-compose down
```

---

### Option 2: Running Locally (Without Docker)

Useful for debugging in your IDE.

1. Ensure you have a local PostgreSQL instance running.
2. Update `.env` so the connection string uses `Host=localhost`:

```env
ConnectionStrings__DefaultConnection="Host=localhost;Database=mafia_chat;..."
```

3. Run the application:

* On **Windows**:

  ```bash
  start.bat
  ```
* On **Linux/macOS**:

  ```bash
  ./start.sh
  ```
* Or directly:

  ```bash
  dotnet run
  ```

---

### Option 3: Docker Hub Image

You can also pull the pre-built Docker image of the service from Docker Hub.

**Docker Hub Repository:** `m1rrerror/mafia-communication-service`

#### Pull and Run

```bash
# Pull the image
docker pull m1rrerror/mafia-communication-service:latest

# Run the container
docker run -d -p 8080:80 --name mafia-communication-service m1rrerror/mafia-communication-service:latest
```

The service will be available at [http://localhost:8080](http://localhost:8080).

#### Notes

* When running via Docker Hub image, you can override environment variables using `-e` flags:

```bash
docker run -d -p 8080:80 \
  -e ConnectionStrings__DefaultConnection="Host=db;Database=mafia_chat;Username=postgres;Password=postgres" \
  --name mafia-communication-service \
  m1rrerror/mafia-communication-service:latest
```

* Stop the container:

```bash
docker stop mafia-communication-service
docker rm mafia-communication-service
```

---

## Running the Tests

From the solution root, run:

```bash
dotnet test --settings coverlet.runsettings
```

---

## API Reference

All request and response bodies are in **JSON** format.

---

### Get Lobby

**Endpoint:** `GET /api/chat/lobby/{lobbyId}`

**Description:** Retrieves the specified lobby.

**Success Response (200):**

```json
{
  "data": {
    "id": "test",
    "privateChannels": {
      "detectives": {
        "name": "detectives",
        "members": {
          "2": true,
          "3": true
        }
      },
      "mafia": {
        "name": "mafia",
        "members": {
          "0": true,
          "1": true
        }
      }
    }
  }
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

### Create Lobby

**Endpoint:** `POST /api/chat/lobby/create`

**Description:** Retrieves the specified lobby.

**Request Body:**

```json
{
    "lobbyId": "test",
    "privateChannels": [
        {
            "channelName": "mafia",
            "memberIds": [0, 1]
        },
        {
            "channelName": "detectives",
            "memberIds": [2, 3]
        }
    ]
}
```

**Success Response (200):**

```json
{
  "data": {
    "id": "test",
    "privateChannels": {
      "detectives": {
        "name": "detectives",
        "members": {
          "2": true,
          "3": true
        }
      },
      "mafia": {
        "name": "mafia",
        "members": {
          "0": true,
          "1": true
        }
      }
    }
  }
}
```

**Error Responses:**

**400 Bad Request**

```json
{
  "error": {
    "code": "LOBBY_EXISTS",
    "message": "Lobby already exists"
  }
}
```


---

### Delete Lobby

**Endpoint:** `DELETE /api/chat/lobby/{lobbyId}`

**Description:** Deletes the specified lobby.

**Success Response (200):**

```json
{
  "data": {
    "message": "Lobby deleted successfully"
  }
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

### Send Global Message

**Endpoint:** `POST /api/chat/global/{lobbyId}/send-message`

**Description:** Sends a message to the specified global chat in the specified lobby.

**URL Parameters:**

* `lobbyId` *(string)* – Unique lobby identifier.

**Request Body:** [ChatMessage Model](#chatmessage-model)

**Success Response (200):**
```json
{
  "data": {
    "lobbyId": "test",
    "senderId": 0,
    "senderName": "mirrerror",
    "content": "test message",
    "timestamp": "2025-10-04T18:27:46.9786613Z"
  }
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
  "data": {
    "channelName": "detectives",
    "lobbyId": "test",
    "senderId": 2,
    "senderName": "mirrerror",
    "content": "test message",
    "timestamp": "2025-10-04T18:28:28.3525069Z"
  }
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

**Description:** Enables/disables (toggles) the global chat in the specified lobby. Broadcasts the new status to all clients in the lobby via WebSocket.

**Success Response (200) - Chat Enabled:**
```json
{
  "data": {
    "lobbyId": "test",
    "isGlobalChatEnabled": true
  }
}
```

**Success Response (200) - Chat Disabled:**
```json
{
  "data": {
    "lobbyId": "test",
    "isGlobalChatEnabled": false
  }
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

### Get Global Chat Status

**Endpoint:** `GET /api/chat/global/{lobbyId}/status`

**Description:** Retrieves the current status of the global chat (enabled/disabled) for the specified lobby.

**URL Parameters:**

* `lobbyId` *(string)* – Unique lobby identifier.

**Success Response (200):**

```json
{
  "data": {
    "lobbyId": "test",
    "isGlobalChatEnabled": true
  }
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

### Get Global Chat History

**Endpoint:** `GET /api/chat/global/{lobbyId}/history`

**Description:** Retrieves the history of messages in the global chat for the specified lobby.

**URL Parameters:**

* `lobbyId` *(string)* – Unique lobby identifier.

**Success Response (200):**

```json
{
  "data": [
    {
      "id": "04abf639-2eac-4711-9792-d48068de45b0",
      "lobbyId": "test",
      "channelName": null,
      "senderId": 0,
      "senderName": "mirrerror",
      "content": "test message",
      "timestamp": "2025-10-04T18:27:46.978661Z"
    }
  ]
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

### Get Private Chat History

**Endpoint:** `GET /api/chat/private/{lobbyId}/{channelName}/history?userId={userId}`

**Description:** Retrieves the message history of a private chat channel for the specified user.

**URL Parameters:**

* `lobbyId` *(string)* – Lobby identifier.
* `channelName` *(string)* – Private channel name.
* `userId` *(long, query)* – The requesting user's ID (used for access validation).

**Success Response (200):**

```json
{
  "data": [
    {
      "id": "4d859f97-b693-4219-b976-684bec8da878",
      "lobbyId": "test",
      "channelName": "detectives",
      "senderId": 2,
      "senderName": "mirrerror",
      "content": "test message",
      "timestamp": "2025-10-04T18:28:28.352506Z"
    }
  ]
}
```

**Error Responses:**

**403 Forbidden**

```json
{
  "error": {
    "code": "ACCESS_DENIED",
    "message": "You do not have access to this private channel's history"
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

**404 Not Found**

```json
{
  "error": {
    "code": "CHANNEL_NOT_FOUND",
    "message": "Channel does not exist"
  }
}
```


---

### Get Private Chat Channels

**Endpoint:** `GET /api/chat/private/{lobbyId}/channels`

**Description:** Retrieves the available private chat channels for the specified lobby.

**Success Response (200):**

```json
{
  "data": [
    "detectives",
    "mafia"
  ]
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

### Send Announcement

**Endpoint:** `POST /api/chat/announcement/{lobbyId}`

**Description:** Sends a real-time announcement to all users in the lobby and saves it.

**Request Body:**

```json
{
  "content": "Night has fallen. Discuss your suspicions!"
}
```

**Success Response (200):**

```json
{
  "data": {
    "id": "0e3d9373-038e-4d03-a5ea-0cd1c4d648db",
    "lobbyId": "test",
    "content": "Night has fallen. Discuss your suspicions!",
    "timestamp": "2025-10-07T18:42:13.521Z"
  }
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

### Get Announcement History

**Endpoint:** `GET /api/chat/announcement/{lobbyId}/history`

**Description:** Returns previously sent announcements for a lobby.

**Success Response (200):**

```json
{
  "data": [
    {
      "id": "0e3d9373-038e-4d03-a5ea-0cd1c4d648db",
      "lobbyId": "test",
      "content": "Night has fallen. Discuss your suspicions!",
      "timestamp": "2025-10-07T18:42:13.521Z"
    }
  ]
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

### Donwload logs

**Endpoint:** `GET /api/logs/download`

**Description:** Download the latest logs.
**Content-Type:** application/octet-stream

**Success Response (200):**
```json
[2025-10-24 18:40:01 INF] Service started.
[2025-10-24 18:42:10 INF] Attempting to download log file from logs/rumours-service.log
```


---

### Get service health status

**Endpoint** `GET /actuator/health`

**Descpription:** Check the service health status.

**Success Response (200):**
```json
{
  "status": "UP",
  "timestamp": "2025-10-24T15:45:00.1234567Z"
}
```


---

## SignalR Hub Reference

**Endpoint**: `WS /chathub`

**Description**: Provides real-time communication between clients and the server.

### Server Methods (Client → Server)

| Method                                                                        | Description                                                                            |
| ----------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `JoinGlobalChat(string lobbyId, long userId)`                                             | Adds the specified user to the global chat in the specified lobby.                                                     |
| `LeaveGlobalChat(string lobbyId, long userId)`                                            | Removes the specified user from the global chat in the specified lobby.                                                |
| `JoinPrivateChannel(string lobbyId, string channelName, long userId)`                      | Adds the specified user to the specified private channel.                                                  |
| `LeavePrivateChannel(string lobbyId, string channelName, long userId)`                     | Removes the speicified user from the specified private channel.                                             |
| `SendGlobalMessage(string lobbyId, ChatMessage message)`                      | Broadcasts a message to the global chat in the specified lobby. Throws `HubException` on validation errors.    |
| `SendPrivateMessage(string channelName, string lobbyId, ChatMessage message)` | Broadcasts a message to the specified private channel in the specified lobby. Throws `HubException` on validation errors. |
| `SendAnnouncement(string lobbyId, AnnouncementDto message)` | Broadcasts a real-time announcement to all in lobby. |

---

### Client Methods (Server → Client)

| Method                                         | Description                                                |
| ---------------------------------------------- | ---------------------------------------------------------- |
| `ReceiveGlobalMessage(ChatResponse response)`  | Triggered when a new message arrives in a global chat.    |
| `ReceivePrivateMessage(ChatResponse response)` | Triggered when a new message arrives in a private channel. |
| `GlobalChatStatusChanged(GlobalChatStatusResponse response)` | Triggered when the global chat status is toggled (enabled/disabled). |
| `ReceiveAnnouncement(Announcement response)` | Triggered when a new announcement is sent to the lobby. |

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

| Field       | Type     | Description               |
|-------------|----------|---------------------------|
| `lobbyId`   | string   | Lobby identifier          |
| `senderId`  | long     | ID of the sender          |
| `senderName`| string   | Name of the sender        |
| `content`   | string   | Message content           |
| `timestamp` | DateTime | UTC timestamp from server |

**Example:**
```json
{
  "lobbyId": "550e8400-e29b-41d4-a716-446655440000",
  "senderId": 123,
  "senderName": "TestUser",
  "content": "Hello, world!",
  "timestamp": "2025-09-09T20:30:00.123Z"
}
```

---

### PrivateChatResponse Model

| Field       | Type     | Description |
|-------------|----------|-------------|
| `channelName` | string | Name of the channel |
| `lobbyId`   | string   | Lobby identifier |
| `senderId`  | long     | ID of the sender |
| `senderName`| string   | Name of the sender |
| `content`   | string   | Message content |
| `timestamp` | DateTime | UTC timestamp from server |

**Example:**
```json
{
  "lobbyId": "550e8400-e29b-41d4-a716-446655440000",
  "channelName": "detectives",
  "senderId": 123,
  "senderName": "TestUser",
  "content": "Hello, world!",
  "timestamp": "2025-09-09T20:30:00.123Z"
}
```

---

### GlobalChatStatusResponse Model

| Field                  | Type    | Description                                |
| ---------------------- | ------- | ------------------------------------------ |
| `lobbyId`              | string  | Lobby identifier                           |
| `isGlobalChatEnabled`  | boolean | Whether global chat is enabled or disabled |

**Example:**

```json
{
  "lobbyId": "test",
  "isGlobalChatEnabled": true
}
```

---

### Announcement Model

| Field       | Type     | Description                           |
| ----------- | -------- | ------------------------------------- |
| `id`        | Guid     | Unique identifier of the announcement |
| `lobbyId`   | string   | Lobby where the announcement was sent |
| `content`   | string   | The announcement message              |
| `timestamp` | DateTime | UTC time the announcement was sent    |

**Example:**

```json
{
  "id": "0e3d9373-038e-4d03-a5ea-0cd1c4d648db",
  "lobbyId": "test",
  "content": "Night has fallen. Discuss your suspicions!",
  "timestamp": "2025-10-07T18:42:13.521Z"
}
```


---

### AnnouncementDto Model

| Field     | Type   | Validation                   |
| --------- | ------ | ---------------------------- |
| `content` | string | Required, max 200 characters |

**Example:**

```json
{
  "content": "Night has fallen. Discuss your suspicions!"
}
```


---

## General Errors

**503 Service Unavailable**

```json
{
  "error": {
    "code": "CONCURRENCY_LIMIT_REACHED",
    "message": "The service is temporarily overloaded. Please try again later."
  }
}
```

**408 Request Timeout**

```json
{
  "error": {
    "code": "REQUEST_TIMEOUT",
    "message": "The request took too long to process."
  }
}
```