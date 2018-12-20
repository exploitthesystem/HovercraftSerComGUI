Hovercraft Serial Communication

A simple serial communication protocol was devised to facilitate transfer of 
data and instructions between a running application on an external CPU 
and a microcontroller (MCU) on a sensor board--designed to be mounted on a hovercraft. 
The protocol specifies a frame of sequential bytes that encapsulates a header and data, 
as described below. This frame encodes information that is used to Each field, 
except data, is fixed length. The numbers below each field specify its length 
in bytes, unless otherwise stated.

Protocol Packet Specification

Frame fields:
[Sync start][Op|W/R][Length][Data][Sync end]
     2         1       1      Var      2

Sync start – A sequence of two bytes are sent at the start of a frame and at the end. 
These are used for sync purposes to allow each device to adjust to an incoming serial stream. 
In keeping with conventional ethernet frames. The start byte sequence is 0xAA55.

Operation/Write-Read – The second field in a frame specifies a 7-bit code and one 
least significant bit (LSB) to determine a read or write operation. 
The 7 most significant bits (MSB) indicate either a target device or a particular 
operation on a device other than a direct read or write. To simplify frame generation at 
the application layer (i.e., high-level communication programs), each 7-bit op code is
represented as a power of 2, leaving the LSB available for read- and write- bit insertion 
without shifting.

Length – The number of bytes in the data field.

Register Start – The sub-address/start register of the IC that is written to or read from on the sensor board.

Data – The data payload that is transmitted or received to and from the sensor board. 
This field can be used to send additional configuration settings and operations, 
thereby allowing greater flexibility than the 1-byte op code R/W field permits.

Sync end – The terminating byte sequence of a frame. The end byte sequence is 0x55AA.
The protocol requires no handshake and assumes an open serial port on which both devices are 
continually listening. Due to the low-level use and simplicity of communicated information, 
no error checking, correction, or retransmission is handled at this level

Serial Tx and Rx

An application on the CPU side provides a serial communication interface for high-level applications. 
This application handles transmission and reception of CPU-MCU frames. For preliminary testing purposes, 
a simple GUI application was created. This application receives user data and generates 
a serial frame that conforms to the specified CPU-MCU protocol. It also receives serial streams from 
the MCU and constitutes them as a frame. The underlying implementation of the testing app serves as 
groundwork for a general communication interface for later hovercraft control and interface programs.

Demonstration 

The GUI of this application has a temperature box to display readings from a remote temperature sensor. 
This temperature sensor is connected to the sensor board via I2C. The console queries the MCU mounted 
on the sensor board for readings every 3 seconds.