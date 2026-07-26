# WWCP EEBus

This software libraries will allow the communication between World Wide Charging Protocol (WWCP)
entities and entities implementing the _EEBus Protocol(s)_, which are defined by the
[_EEBus Initiative e.V._](https://www.eebus.org). EEBus enables energy management relevant devices
in buildings to connect and interact with each other and with grid and market operators.

For more details on the *EEBus* protocol family please visit https://www.eebus.org.


## Smart Home IP (SHIP)

SHIP is the communication protocol/infrastructure within the *EEBus* protcol family.
It is based on Multicast DNS Service Discovery and HTTPS WebSockets between devices in home area
networks, but also towards the grid or cloud services. SHIP also defines how to make use of X.509
certificates for mutual authentication and encryption of communication channels.

In contrast to the WWCP, SHIP does ***NOT*** define an *overlay network*. There is also no routing
or forwarding of messages defined.


## Smart Premises Interoperable Neutral-Message Exchange (SPINE)

SPINE is the higher level protocol of the *EEBus* family and runs on top of SHIP.
It defines data structures and message exchanges between devices.


## Repository layout

| Project | Content |
|---------|---------|
| `WWCP_EEBus_SHIP` | SHIP 1.0.1: messages, EEBus-JSON, message exchange state machines, mDNS discovery, certificates/SKI |
| `WWCP_EEBus_SPINE` | SPINE 1.3.0: data model, device/entity/feature model, node management |
| `WWCP_EEBus_UseCases` | EEBus use cases (LPC, LPP, MPC, MGCP, OPEV, OSCEV, ...) |
| `*_Tests` | NUnit test suites |
| `WWCP_EEBus_Adapter` | **Parked**, not part of the solution: the earlier WWCP/OverlayNetworking integration layer, see its own README |

The SHIP/SPINE/UseCases stack is standalone: it only builds on
[Styx](https://github.com/Vanaheimr/Styx) and [Hermod](https://github.com/Vanaheimr/Hermod),
which are expected as sibling checkouts. This keeps the protocol implementation
independently testable; the bridge into WWCP will be re-added later via the adapter project.

Conformance and interoperability test suites for this stack (including simulations for the
common e-mobility use cases and interop runs against ship-go/spine-go/eebus-go) live in
[EEBusConformanceTests](https://github.com/OpenChargingCloud/EEBusConformanceTests).

All timing relevant components (SHIP handshake timers, heartbeats, failsafe durations, ...)
are driven by a `System.TimeProvider`, never by the wall clock, so that protocol timing can be
tested deterministically.



### Your participation

This software is Open Source under the **Apache 2.0 license** and in some parts **Affero GPL 3.0 license**. We appreciate your participation in this ongoing project, and your help to improve it and the e-mobility ICT in general. If you find bugs, want to request a feature or send us a pull request, feel free to use the normal GitHub features to do so. For this please read the Contributor License Agreement carefully and send us a signed copy or use a similar free and open license.
