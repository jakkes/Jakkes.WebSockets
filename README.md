# WebSockets for .NET Core.

Nuget installation:

`Install-Package Jakkes.WebSockets`

## Usage

Everything is in the namespace `Jakkes.WebSockets.Server`. The server class creates a `Connection`-object on connection through the `OnClientConnect`-event. However, using this object is not necessary, as can be seen in the examples.
