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
*
* File    : SEGGER_RTT.c
* Purpose : Implementation of SEGGER Real-Time Transfer (RTT) which
*           allows real-time communication on targets which support
*           debugger memory accesses while the CPU is running.
*
* NOTE: This is a self-contained, portable implementation compatible
*       with J-Link RTT Viewer protocol. The host-side tool (J-Link,
*       RTT Viewer) locates the _SEGGER_RTT control block by scanning
*       target RAM for the "SEGGER RTT" magic string.
*
* Revision: $Rev: 25842 $
*/

#include "SEGGER_RTT.h"
#include <string.h>

/*********************************************************************
*
*       Defines
*
**********************************************************************
*/

/* Compiler barrier - prevent reordering around RTT buffer writes */
#if defined(__GNUC__) || defined(__clang__)
  #define SEGGER_RTT_MEMBARRIER()  __asm volatile("" ::: "memory")
#else
  #define SEGGER_RTT_MEMBARRIER()
#endif

/*********************************************************************
*
*       Static const data
*
**********************************************************************
*/

/* Up-buffer (Target -> Host) storage */
static char _acUpBuffer[BUFFER_SIZE_UP];

/* Down-buffer (Host -> Target) storage */
static char _acDownBuffer[BUFFER_SIZE_DOWN];

/*********************************************************************
*
*       Global data
*
**********************************************************************
*/

/*
 * _SEGGER_RTT is placed at a known location so the J-Link DLL / RTT Viewer
 * can find it by scanning target RAM for the 16-byte magic "SEGGER RTT\0\0\0\0\0\0".
 * Keep the acID field as the very first member.
 */
SEGGER_RTT_CB _SEGGER_RTT = {
  "SEGGER RTT",                               /* acID - magic signature (must be exactly 16 bytes) */
  SEGGER_RTT_MAX_NUM_UP_BUFFERS,              /* MaxNumUpBuffers   */
  SEGGER_RTT_MAX_NUM_DOWN_BUFFERS,            /* MaxNumDownBuffers */
  {
    /* aUp[0] - Terminal channel */
    { "Terminal", _acUpBuffer, sizeof(_acUpBuffer), 0u, 0u, SEGGER_RTT_MODE_DEFAULT },
    /* aUp[1..N-1] - unused, zero-initialised */
  },
  {
    /* aDown[0] - Terminal input channel */
    { "Terminal", _acDownBuffer, sizeof(_acDownBuffer), 0u, 0u, SEGGER_RTT_MODE_DEFAULT },
    /* aDown[1..N-1] - unused, zero-initialised */
  }
};

/*********************************************************************
*
*       Static helper functions
*
**********************************************************************
*/

/*********************************************************************
*
*       _WriteBlocking
*
*  Function description
*    Stores a specified number of characters in SEGGER RTT ring buffer
*    and waits if required.
*/
static unsigned _WriteBlocking(SEGGER_RTT_BUFFER_UP* pRing, const char* pData, unsigned NumBytes)
{
  unsigned NumBytesToWrite;
  unsigned NumBytesWritten;
  unsigned RdOff;
  unsigned WrOff;

  NumBytesWritten = 0u;
  WrOff = pRing->WrOff;

  do {
    RdOff = pRing->RdOff;                     /* May be changed by host (volatile) */
    if (RdOff > WrOff) {
      NumBytesToWrite = RdOff - WrOff - 1u;
    } else {
      NumBytesToWrite = pRing->SizeOfBuffer - (WrOff - RdOff + 1u);
    }
    NumBytesToWrite = (NumBytesToWrite < (NumBytes - NumBytesWritten))
                      ? NumBytesToWrite : (NumBytes - NumBytesWritten);

    /* Copy data into ring buffer */
    if ((WrOff + NumBytesToWrite) <= pRing->SizeOfBuffer) {
      memcpy(pRing->pBuffer + WrOff, pData + NumBytesWritten, NumBytesToWrite);
      WrOff += NumBytesToWrite;
      if (WrOff == pRing->SizeOfBuffer) {
        WrOff = 0u;
      }
    } else {
      /* Wrap-around case */
      unsigned Rem = pRing->SizeOfBuffer - WrOff;
      memcpy(pRing->pBuffer + WrOff, pData + NumBytesWritten, Rem);
      memcpy(pRing->pBuffer,         pData + NumBytesWritten + Rem, NumBytesToWrite - Rem);
      WrOff = NumBytesToWrite - Rem;
    }
    SEGGER_RTT_MEMBARRIER();
    pRing->WrOff = WrOff;
    NumBytesWritten += NumBytesToWrite;
  } while (NumBytesWritten < NumBytes);

  return NumBytesWritten;
}

/*********************************************************************
*
*       _WriteNoCheck
*
*  Function description
*    Stores a specified number of characters in SEGGER RTT ring buffer.
*    DOES NOT check whether there is enough space — caller must check.
*/
static void _WriteNoCheck(SEGGER_RTT_BUFFER_UP* pRing, const char* pData, unsigned NumBytes)
{
  unsigned WrOff;
  unsigned Rem;

  WrOff = pRing->WrOff;
  Rem   = pRing->SizeOfBuffer - WrOff;

  if (Rem > NumBytes) {
    memcpy(pRing->pBuffer + WrOff, pData, NumBytes);
    SEGGER_RTT_MEMBARRIER();
    pRing->WrOff = WrOff + NumBytes;
  } else {
    /* Wrap-around */
    memcpy(pRing->pBuffer + WrOff, pData, Rem);
    memcpy(pRing->pBuffer,         pData + Rem, NumBytes - Rem);
    SEGGER_RTT_MEMBARRIER();
    pRing->WrOff = NumBytes - Rem;
  }
}

/*********************************************************************
*
*       _GetAvailWriteSpace
*/
static unsigned _GetAvailWriteSpace(SEGGER_RTT_BUFFER_UP* pRing)
{
  unsigned RdOff;
  unsigned WrOff;
  unsigned r;

  RdOff = pRing->RdOff;
  WrOff = pRing->WrOff;
  if (RdOff <= WrOff) {
    r = pRing->SizeOfBuffer - 1u - WrOff + RdOff;
  } else {
    r = RdOff - WrOff - 1u;
  }
  return r;
}

/*********************************************************************
*
*       Public functions
*
**********************************************************************
*/

/*********************************************************************
*
*       SEGGER_RTT_Init
*
*  Function description
*    Initialises the RTT control block.
*    Should be called before first use of any RTT API functions.
*    Re-entrant safe — safe to call more than once.
*/
void SEGGER_RTT_Init(void)
{
  /* Ensure the magic ID is correctly set (guard against BSS zero-init
     if the control block is placed in a zero-initialised section). */
  if (_SEGGER_RTT.acID[0] != 'S') {
    /* Re-initialise from scratch */
    memcpy(&_SEGGER_RTT.acID[0], "SEGGER RTT", 10u);
    memset(&_SEGGER_RTT.acID[10], 0, 6u);

    _SEGGER_RTT.MaxNumUpBuffers   = SEGGER_RTT_MAX_NUM_UP_BUFFERS;
    _SEGGER_RTT.MaxNumDownBuffers = SEGGER_RTT_MAX_NUM_DOWN_BUFFERS;

    /* Configure default up-buffer (channel 0) */
    _SEGGER_RTT.aUp[0].sName        = "Terminal";
    _SEGGER_RTT.aUp[0].pBuffer      = _acUpBuffer;
    _SEGGER_RTT.aUp[0].SizeOfBuffer = sizeof(_acUpBuffer);
    _SEGGER_RTT.aUp[0].RdOff        = 0u;
    _SEGGER_RTT.aUp[0].WrOff        = 0u;
    _SEGGER_RTT.aUp[0].Flags        = SEGGER_RTT_MODE_DEFAULT;

    /* Configure default down-buffer (channel 0) */
    _SEGGER_RTT.aDown[0].sName        = "Terminal";
    _SEGGER_RTT.aDown[0].pBuffer      = _acDownBuffer;
    _SEGGER_RTT.aDown[0].SizeOfBuffer = sizeof(_acDownBuffer);
    _SEGGER_RTT.aDown[0].RdOff        = 0u;
    _SEGGER_RTT.aDown[0].WrOff        = 0u;
    _SEGGER_RTT.aDown[0].Flags        = SEGGER_RTT_MODE_DEFAULT;
  }
}

/*********************************************************************
*
*       SEGGER_RTT_ConfigUpBuffer
*
*  Function description
*    Configures or reconfigures an up-buffer (Target -> Host).
*    BufferIndex 0 is the default "Terminal" channel.
*    Pass pBuffer = NULL to keep the default static buffer.
*/
int SEGGER_RTT_ConfigUpBuffer(unsigned BufferIndex, const char* sName, void* pBuffer, unsigned BufferSize, unsigned Flags)
{
  int r = 0;

  if (BufferIndex < (unsigned)_SEGGER_RTT.MaxNumUpBuffers) {
    SEGGER_RTT_BUFFER_UP* pUp = &_SEGGER_RTT.aUp[BufferIndex];

    if (sName)   { pUp->sName  = sName; }
    if (pBuffer) {
      pUp->pBuffer      = (char*)pBuffer;
      pUp->SizeOfBuffer = BufferSize;
      pUp->RdOff        = 0u;
      pUp->WrOff        = 0u;
    }
    pUp->Flags = Flags;
  } else {
    r = -1;
  }
  return r;
}

/*********************************************************************
*
*       SEGGER_RTT_ConfigDownBuffer
*/
int SEGGER_RTT_ConfigDownBuffer(unsigned BufferIndex, const char* sName, void* pBuffer, unsigned BufferSize, unsigned Flags)
{
  int r = 0;

  if (BufferIndex < (unsigned)_SEGGER_RTT.MaxNumDownBuffers) {
    SEGGER_RTT_BUFFER_DOWN* pDown = &_SEGGER_RTT.aDown[BufferIndex];

    if (sName)   { pDown->sName  = sName; }
    if (pBuffer) {
      pDown->pBuffer      = (char*)pBuffer;
      pDown->SizeOfBuffer = BufferSize;
      pDown->RdOff        = 0u;
      pDown->WrOff        = 0u;
    }
    pDown->Flags = Flags;
  } else {
    r = -1;
  }
  return r;
}

/*********************************************************************
*
*       SEGGER_RTT_Write
*
*  Function description
*    Stores a specified number of characters in SEGGER RTT ring buffer
*    and sets the write pointer.
*
*  Returns
*    Number of bytes actually written (may be less when NO_BLOCK_SKIP).
*/
unsigned SEGGER_RTT_Write(unsigned BufferIndex, const void* pBuffer, unsigned NumBytes)
{
  unsigned           Status;
  unsigned           Avail;
  SEGGER_RTT_BUFFER_UP* pRing;

  if (BufferIndex >= (unsigned)_SEGGER_RTT.MaxNumUpBuffers || NumBytes == 0u) {
    return 0u;
  }

  pRing = &_SEGGER_RTT.aUp[BufferIndex];
  Avail = _GetAvailWriteSpace(pRing);

  switch (pRing->Flags & SEGGER_RTT_MODE_MASK) {
    case SEGGER_RTT_MODE_NO_BLOCK_SKIP:
      if (Avail < NumBytes) {
        Status = 0u;          /* Not enough space — skip entirely */
      } else {
        _WriteNoCheck(pRing, (const char*)pBuffer, NumBytes);
        Status = NumBytes;
      }
      break;

    case SEGGER_RTT_MODE_NO_BLOCK_TRIM:
      if (Avail < NumBytes) {
        NumBytes = Avail;
      }
      _WriteNoCheck(pRing, (const char*)pBuffer, NumBytes);
      Status = NumBytes;
      break;

    case SEGGER_RTT_MODE_BLOCK_IF_FIFO_FULL:
      Status = _WriteBlocking(pRing, (const char*)pBuffer, NumBytes);
      break;

    default:
      Status = 0u;
      break;
  }

  return Status;
}

/*********************************************************************
*
*       SEGGER_RTT_WriteNoLock
*
*  Same as SEGGER_RTT_Write but without disabling interrupts.
*  Use only when calling from a context where you are certain no other
*  context can preempt and write to the same buffer.
*/
unsigned SEGGER_RTT_WriteNoLock(unsigned BufferIndex, const void* pBuffer, unsigned NumBytes)
{
  return SEGGER_RTT_Write(BufferIndex, pBuffer, NumBytes);
}

/*********************************************************************
*
*       SEGGER_RTT_WriteString
*/
unsigned SEGGER_RTT_WriteString(unsigned BufferIndex, const char* s)
{
  unsigned Len;
  Len = (unsigned)strlen(s);
  return SEGGER_RTT_Write(BufferIndex, s, Len);
}

/*********************************************************************
*
*       SEGGER_RTT_PutChar
*/
unsigned SEGGER_RTT_PutChar(unsigned BufferIndex, char c)
{
  return SEGGER_RTT_Write(BufferIndex, &c, 1u);
}

/*********************************************************************
*
*       SEGGER_RTT_Read
*
*  Function description
*    Reads characters from SEGGER RTT ring buffer.
*
*  Returns
*    Number of bytes read.
*/
int SEGGER_RTT_Read(unsigned BufferIndex, void* pBuffer, unsigned BufferSize)
{
  unsigned                NumBytesRem;
  unsigned                NumBytesRead;
  unsigned                RdOff;
  unsigned                WrOff;
  unsigned char*          pData;
  SEGGER_RTT_BUFFER_DOWN* pRing;

  if (BufferIndex >= (unsigned)_SEGGER_RTT.MaxNumDownBuffers) {
    return 0;
  }

  pRing        = &_SEGGER_RTT.aDown[BufferIndex];
  pData        = (unsigned char*)pBuffer;
  RdOff        = pRing->RdOff;
  WrOff        = pRing->WrOff;
  NumBytesRead = 0u;

  while (RdOff != WrOff && NumBytesRead < BufferSize) {
    NumBytesRem = (WrOff >= RdOff) ? (WrOff - RdOff) : (pRing->SizeOfBuffer - RdOff);
    NumBytesRem = (NumBytesRem < (BufferSize - NumBytesRead)) ? NumBytesRem : (BufferSize - NumBytesRead);
    memcpy(pData + NumBytesRead, pRing->pBuffer + RdOff, NumBytesRem);
    NumBytesRead += NumBytesRem;
    RdOff        += NumBytesRem;
    if (RdOff >= pRing->SizeOfBuffer) {
      RdOff = 0u;
    }
    WrOff = pRing->WrOff;   /* Re-read in case host wrote more */
  }
  pRing->RdOff = RdOff;
  return (int)NumBytesRead;
}

/*********************************************************************
*
*       SEGGER_RTT_HasData
*/
int SEGGER_RTT_HasData(unsigned BufferIndex)
{
  SEGGER_RTT_BUFFER_DOWN* pRing;

  if (BufferIndex >= (unsigned)_SEGGER_RTT.MaxNumDownBuffers) {
    return 0;
  }
  pRing = &_SEGGER_RTT.aDown[BufferIndex];
  return (pRing->WrOff != pRing->RdOff) ? 1 : 0;
}

/*********************************************************************
*
*       SEGGER_RTT_GetKey
*
*  Returns
*    >= 0 if a byte is available (the byte value), -1 if no data.
*/
int SEGGER_RTT_GetKey(void)
{
  unsigned char c;
  int r;
  r = SEGGER_RTT_Read(0u, &c, 1u);
  return (r == 1) ? (int)c : -1;
}

/*********************************************************************
*
*       SEGGER_RTT_WaitKey
*
*  Blocks until a key is received.
*/
int SEGGER_RTT_WaitKey(void)
{
  int r;
  do {
    r = SEGGER_RTT_GetKey();
  } while (r < 0);
  return r;
}

/*********************************************************************
*
*       SEGGER_RTT_HasKey
*/
int SEGGER_RTT_HasKey(void)
{
  return SEGGER_RTT_HasData(0u);
}

/*********************************************************************
*
*       SEGGER_RTT_SetTerminal
*/
int SEGGER_RTT_SetTerminal(unsigned char TerminalId)
{
  (void)TerminalId;
  /* Terminal switching via escape codes is handled by the host-side tool */
  return 0;
}

/*********************************************************************
*
*       SEGGER_RTT_TerminalOut
*/
int SEGGER_RTT_TerminalOut(unsigned char TerminalId, const char* s)
{
  (void)TerminalId;
  return (int)SEGGER_RTT_WriteString(0u, s);
}

/*********************************************************************
*
*       SEGGER_RTT_vprintf
*/
int SEGGER_RTT_vprintf(unsigned BufferIndex, const char* sFormat, va_list* pParamList)
{
  char buf[SEGGER_RTT_PRINTF_BUFFER_SIZE];
  int  len;

  len = vsnprintf(buf, sizeof(buf), sFormat, *pParamList);
  if (len > 0) {
    SEGGER_RTT_Write(BufferIndex, buf, (unsigned)len);
  }
  return len;
}

/*********************************************************************
*
*       SEGGER_RTT_printf
*/
int SEGGER_RTT_printf(unsigned BufferIndex, const char* sFormat, ...)
{
  int     r;
  va_list ParamList;

  va_start(ParamList, sFormat);
  r = SEGGER_RTT_vprintf(BufferIndex, sFormat, &ParamList);
  va_end(ParamList);
  return r;
}

/*************************** End of file ****************************/
