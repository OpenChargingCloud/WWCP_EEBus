# WWCP EEBus

This software libraries will allow the communication between World Wide Charging Protocol (WWCP)
entities and entities implementing the _EEBus Protocol(s)_, which are defined by the
[_EEBus Initiative e.V._](https://www.eebus.org). EEBus enables energy management relevant devices
in buildings to connect and interact with each other and with grid and market operators.

For more details on the *EEBus* protocol family please visit https://www.eebus.org.


## Smart Home IP (SHIP)

SHIP is the communication protocol/infrastructure within the *EEBus* protcol family.
It is based on Multicast DNS Service Discovery and HTTPS Web Sockets between devices in home area
networks, but also towards the grid or cloud services. SHIP also defines how to make use of X.509
certificates for mutual authentication and encryption of communication channels.

In contrast to the WWCP, SHIP does ***NOT*** define an *overlay network*. There is also no routing
or forwarding of messages defined.


## Smart Premises Interoperable Neutral-Message Exchange (SPINE)

SPINE is the higher level protocol of the *EEBus* family and runs on top of SHIP.
It defines data structures and message exchanges between devices.



### Your participation

This software is Open Source under the **Apache 2.0 license** and in some parts **Affero GPL 3.0 license**. We appreciate your participation in this ongoing project, and your help to improve it and the e-mobility ICT in general. If you find bugs, want to request a feature or send us a pull request, feel free to use the normal GitHub features to do so. For this please read the Contributor License Agreement carefully and send us a signed copy or use a similar free and open license.
