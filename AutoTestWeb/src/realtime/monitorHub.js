import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

let connection = null;
let starting = null;

export function getMonitorHubConnection() {
  if (connection) return connection;

  connection = new HubConnectionBuilder()
    .withUrl("/hubs/monitor", {
      accessTokenFactory: () => localStorage.getItem("accessToken") || ""
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  return connection;
}

export async function ensureMonitorHubStarted() {
  const conn = getMonitorHubConnection();
  if (conn.state === "Connected") return conn;
  if (starting) return starting;

  starting = conn
    .start()
    .then(() => conn)
    .finally(() => {
      starting = null;
    });

  return starting;
}

export async function stopMonitorHub() {
  if (!connection) return;
  try {
    await connection.stop();
  } finally {
    connection = null;
    starting = null;
  }
}

