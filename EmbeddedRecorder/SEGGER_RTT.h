/*********************************************************************
*                    SEGGER Microcontroller GmbH                     *
*                        The Embedded Experts                        *
**********************************************************************
*                                                                    *
*            (c) 1995 - 2021 SEGGER Microcontroller GmbH            *
*                                                                    *
*       www.segger.com     Support: support@segger.com               *
*                                                                    *
**********************************************************************
*                                                                    *
*       SEGGER RTT * Real Time Transfer for embedded targets         *
*                                                                    *
**********************************************************************
*                                                                    *
* All rights reserved.                                               *
*                                                                    *
* SEGGER strongly recommends to not make any changes                 *
* to or modify the source code of this software in order to stay    *
* compatible with the RTT protocol and J-Link.                       *
*                                                                    *
* Redistribution and use in source and binary forms, with or        *
* without modification, are permitted provided that the following   *
* condition is met:                                                  *
*                                                                    *
* o Redistributions of source code must retain the above copyright  *
*   notice, this condition and the following disclaimer.             *
*                                                                    *
* THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND            *
* CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,       *
* INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF          *
* MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE          *
* DISCLAIMED. IN NO EVENT SHALL SEGGER Microcontroller BE LIABLE FOR*
* ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR          *
* CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT *
* OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;  *
* OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF    *
* LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT         *
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE *
* USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH  *
* DAMAGE.                                                            *
*                                                                    *
**********************************************************************
---------------------------END-OF-HEADER------------------------------
File    : SEGGER_RTT.h
Purpose : Implementation of SEGGER real-time transfer which allows
          real-time communication on targets which support debugger
          memory accesses while the CPU is running.
Revision: $Rev: 25842 $
----------------------------------------------------------------------
*/

#ifndef SEGGER_RTT_H
#define SEGGER_RTT_H

#include <stdarg.h>
#include <stdint.h>

/*********************************************************************
*
*       Defines, configurable
*
**********************************************************************
*/

#ifndef   SEGGER_RTT_MAX_NUM_UP_BUFFERS
  #define SEGGER_RTT_MAX_NUM_UP_BUFFERS           (3)     // Max. number of up-buffers (T->H) available on this target
#endif

#ifndef   SEGGER_RTT_MAX_NUM_DOWN_BUFFERS
  #define SEGGER_RTT_MAX_NUM_DOWN_BUFFERS         (3)     // Max. number of down-buffers (H->T) available on this target
#endif

#ifndef   BUFFER_SIZE_UP
  #define BUFFER_SIZE_UP                          (1024)  // Size of the buffer for terminal output of target, up to host
#endif

#ifndef   BUFFER_SIZE_DOWN
  #define BUFFER_SIZE_DOWN                        (16)    // Size of the buffer for terminal input to target from host
#endif

#ifndef   SEGGER_RTT_PRINTF_BUFFER_SIZE
  #define SEGGER_RTT_PRINTF_BUFFER_SIZE           (64u)   // Size of buffer for RTT printf to bulk-send chars via RTT
#endif

#ifndef   SEGGER_RTT_MODE_DEFAULT
  #define SEGGER_RTT_MODE_DEFAULT                 SEGGER_RTT_MODE_NO_BLOCK_SKIP
#endif

/*********************************************************************
*
*       RTT transfer modes
*
**********************************************************************
*/
#define SEGGER_RTT_MODE_NO_BLOCK_SKIP         (0)   // Skip. Do not block, output nothing.
#define SEGGER_RTT_MODE_NO_BLOCK_TRIM         (1)   // Trim: Do not block, output as much as fits.
#define SEGGER_RTT_MODE_BLOCK_IF_FIFO_FULL    (2)   // Block: Wait until there is space in the buffer.
#define SEGGER_RTT_MODE_MASK                  (3)

/*********************************************************************
*
*       RTT Control Block
*
**********************************************************************
*/
typedef struct {
  const char*    sName;         // Optional name. Standard names so far are: "Terminal", "SysView", "J-Scope_t4i4"
  char*          pBuffer;       // Pointer to start of buffer
  unsigned int   SizeOfBuffer;  // Buffer size in bytes. Note that one byte is lost, as this implementation does not fill up the buffer in order to avoid the overflow.
  unsigned int   WrOff;         // Position of next item to be written by either target.
  volatile unsigned int RdOff;  // Position of next item to be read by host. Must be volatile since it may be modified by host.
  unsigned int   Flags;         // Contains configuration flags
} SEGGER_RTT_BUFFER_UP;

typedef struct {
  const char*    sName;         // Optional name. Standard names so far are: "Terminal", "SysView", "J-Scope_t4i4"
  char*          pBuffer;       // Pointer to start of buffer
  unsigned int   SizeOfBuffer;  // Buffer size in bytes. Note that one byte is lost, as this implementation does not fill up the buffer in order to avoid the overflow.
  volatile unsigned int WrOff;  // Position of next item to be written by host. Must be volatile since it may be modified by host.
  unsigned int   RdOff;         // Position of next item to be read by target (down-buffer).
  unsigned int   Flags;         // Contains configuration flags
} SEGGER_RTT_BUFFER_DOWN;

typedef struct {
  char                    acID[16];                                 // Initialized to "SEGGER RTT"
  int                     MaxNumUpBuffers;                          // Initialized to SEGGER_RTT_MAX_NUM_UP_BUFFERS
  int                     MaxNumDownBuffers;                        // Initialized to SEGGER_RTT_MAX_NUM_DOWN_BUFFERS
  SEGGER_RTT_BUFFER_UP    aUp[SEGGER_RTT_MAX_NUM_UP_BUFFERS];       // Up buffers, transferring information up from target via debug probe to host
  SEGGER_RTT_BUFFER_DOWN  aDown[SEGGER_RTT_MAX_NUM_DOWN_BUFFERS];   // Down buffers, transferring information down from host via debug probe to target
} SEGGER_RTT_CB;

/*********************************************************************
*
*       Global data
*
**********************************************************************
*/
extern SEGGER_RTT_CB _SEGGER_RTT;

/*********************************************************************
*
*       RTT API functions
*
**********************************************************************
*/
#ifdef __cplusplus
  extern "C" {
#endif

void          SEGGER_RTT_Init           (void);
int           SEGGER_RTT_Read           (unsigned BufferIndex,       void* pBuffer, unsigned BufferSize);
unsigned      SEGGER_RTT_Write          (unsigned BufferIndex, const void* pBuffer, unsigned NumBytes);
unsigned      SEGGER_RTT_WriteString    (unsigned BufferIndex, const char* s);
unsigned      SEGGER_RTT_WriteNoLock    (unsigned BufferIndex, const void* pBuffer, unsigned NumBytes);
unsigned      SEGGER_RTT_PutChar        (unsigned BufferIndex, char c);
int           SEGGER_RTT_GetKey         (void);
int           SEGGER_RTT_WaitKey        (void);
int           SEGGER_RTT_HasKey         (void);
int           SEGGER_RTT_HasData        (unsigned BufferIndex);
int           SEGGER_RTT_ConfigUpBuffer (unsigned BufferIndex, const char* sName, void* pBuffer, unsigned BufferSize, unsigned Flags);
int           SEGGER_RTT_ConfigDownBuffer(unsigned BufferIndex, const char* sName, void* pBuffer, unsigned BufferSize, unsigned Flags);

/* Terminal control */
int           SEGGER_RTT_SetTerminal    (unsigned char TerminalId);
int           SEGGER_RTT_TerminalOut    (unsigned char TerminalId, const char* s);

/* printf-style */
int           SEGGER_RTT_printf         (unsigned BufferIndex, const char* sFormat, ...);
int           SEGGER_RTT_vprintf        (unsigned BufferIndex, const char* sFormat, va_list* pParamList);

#ifdef __cplusplus
  }
#endif

/*********************************************************************
*
*       Macros
*
**********************************************************************
*/
#define RTT_CTRL_RESET        "\x1B[0m"
#define RTT_CTRL_CLEAR        "\x1B[2J"
#define RTT_CTRL_TEXT_BLACK   "\x1B[2;30m"
#define RTT_CTRL_TEXT_RED     "\x1B[2;31m"
#define RTT_CTRL_TEXT_GREEN   "\x1B[2;32m"
#define RTT_CTRL_TEXT_YELLOW  "\x1B[2;33m"
#define RTT_CTRL_TEXT_BLUE    "\x1B[2;34m"
#define RTT_CTRL_TEXT_MAGENTA "\x1B[2;35m"
#define RTT_CTRL_TEXT_CYAN    "\x1B[2;36m"
#define RTT_CTRL_TEXT_WHITE   "\x1B[2;37m"

#endif /* SEGGER_RTT_H */

/*************************** End of file ****************************/
