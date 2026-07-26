# WWCP_EEBus_Adapter (parked)

This folder holds the **WWCP integration layer** of the original EEBus prototype.
It is *not* part of `WWCP_EEBus.sln` and is currently **not built**.

## Why

The SHIP/SPINE/UseCases core is being built as a standalone protocol stack on top of
[Styx](https://github.com/Vanaheimr/Styx) and [Hermod](https://github.com/Vanaheimr/Hermod)
only, so that it can be developed and conformance-tested without dragging in
`WWCP_Core` / `WWCP_OverlayNetworking`.

Everything in here depends on those two projects and was, in parts, copied over from
the OCPP stack (networking node ids, networking modes, request/response forwarding).

## Contents

| Path | Original purpose |
|------|------------------|
| `EEBusAdapter/` | IN/OUT/FORWARD message adapter modelled after the OCPP WebSocket adapter |
| `AEEBusNode.cs`, `IEEBusNetworkingNode.cs` | EEBus node built on `AOverlayNetworkingNode` |
| `WebSocket/SHIPWebSocketClient.cs`, `WebSocket/SHIPWebSocketServer.cs` | WebSocket client/server carrying `NetworkingMode`, node registries etc. |

## What happens next

The new core implements the SHIP transport directly on Hermod's WebSocket client/server
(see `ARBEITSPLAN.md`, WP04/WP05). Once it is complete, this adapter will be revived on
top of it to bridge EEBus into WWCP — then with `WWCP_Core` available as a sibling
checkout again.

Until then the code stays here as reference material; do not extend it.
