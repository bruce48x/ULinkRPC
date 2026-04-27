# Agar.MixedTransport

Godot client sample that uses two transports in one flow:

- `TCP` for login/authentication
- `KCP` for battle session traffic

The client starts on a login screen. After login succeeds, the server returns a `token` plus a pre-assigned KCP `conv`. The client reconnects to the battle server with that `conv` and enters a lightweight `agar.io`-style arena.

## Structure

- `Shared`: contracts and DTOs used by both server and Godot client
- `Agar.MixedTransport.Server`: .NET 10 dual-endpoint sample server
- `Agar.MixedTransport.Godot`: Godot 4.6 C# client

## Quick Start

Generate binders and build the server:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample Agar.MixedTransport
```

Run the server:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample Agar.MixedTransport -Run
```

Default ports:

- TCP login: `20000`
- KCP battle: `20001`

Open `samples/Agar.MixedTransport/Agar.MixedTransport.Godot` in Godot 4.6 Mono, build once, then run `Main.tscn`.
