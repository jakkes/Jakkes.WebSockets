# WebSockets for .NET Core.

A WebSocket server for .NET Core written in C#.

## Installation

### .NET CLI
`dotnet add package Jakkes.WebSockets --version 2.0.0-alpha`

### Package Manager
`Install-Package Jakkes.WebSockets -Version 2.0.0-alpha`

## Usage

Very straight forward. Create or derive the class `WebSocketServer` in the `Jakkes.WebSockets.Server` namespace. Register to the `ClientConnected` event and you are good to go!  
Don't forget to start the server by calling `Start()`.  
  
## Demo
See `EchoExample` and `ChatExample` for super tiny examples.