import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

export function createTicketHubConnection(token: string) {
  return new HubConnectionBuilder()
    .withUrl("/hubs/tickets", {
      accessTokenFactory: () => token
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
