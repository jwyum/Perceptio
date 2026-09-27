#include "trcRecorder.h"
#include <stdio.h>
#include <stdarg.h>

#if defined(USE_SEGGER_RTT)
#include "SEGGER_RTT.h"
#endif

/* S32K344 (Cortex-M7) Core Clock in MHz (Default 160MHz) */
static uint32_t s_trace_cpu_mhz = 160;

#if !defined(USE_SEGGER_RTT)
/* Optional fallback write hook if not using direct RTT */
__attribute__((weak)) void vHardwareTraceWrite(const uint8_t* pData, uint16_t length)
{
    (void)pData;
    (void)length;
}
#endif

void vTraceRecorderInit(uint32_t cpu_freq_hz)
{
    if (cpu_freq_hz > 0)
    {
        s_trace_cpu_mhz = cpu_freq_hz / 1000000UL;
        if (s_trace_cpu_mhz == 0) s_trace_cpu_mhz = 1;
    }

    /* Cortex-M7 (S32K344) DWT Cycle Counter Initialization */
    CoreDebug->DEMCR |= CoreDebug_DEMCR_TRCENA_Msk;

    /* Cortex-M7 Core has a Software Lock for DWT registers.
       Must unlock with 0xC5ACCE55 before modifying DWT->CTRL */
#ifdef DWT_LAR_KEY
    DWT->LAR = DWT_LAR_KEY;
#elif defined(DWT)
    /* 0xC5ACCE55 is the CoreSight Software Lock unlock key */
    *((volatile uint32_t*)((uint32_t)DWT + 0xFB0)) = 0xC5ACCE55UL;
#endif

    DWT->CTRL |= DWT_CTRL_CYCCNTENA_Msk;
    DWT->CYCCNT = 0;

#if defined(USE_SEGGER_RTT)
    /* Initialize RTT and configure Up-Buffer 0 for non-blocking stream */
    SEGGER_RTT_Init();
    SEGGER_RTT_ConfigUpBuffer(0, "Tracealyzer", NULL, 0, SEGGER_RTT_MODE_NO_BLOCK_SKIP);
#endif
}

uint32_t ulTraceGetMicroseconds(void)
{
    return DWT->CYCCNT / s_trace_cpu_mhz;
}

void vTraceRecordEvent(uint32_t ts_us, const char* event_type, uint32_t actor_id, const char* name, uint32_t prio, const char* details)
{
    char buffer[128];
    int len = snprintf(buffer, sizeof(buffer), "%lu,%s,%lu,%s,%lu,%s\n",
                       (unsigned long)ts_us,
                       event_type ? event_type : "UserEvent",
                       (unsigned long)actor_id,
                       name ? name : "Unknown",
                       (unsigned long)prio,
                       details ? details : "");
    if (len > 0)
    {
#if defined(USE_SEGGER_RTT)
        SEGGER_RTT_Write(0, buffer, (unsigned)len);
#else
        vHardwareTraceWrite((const uint8_t*)buffer, (uint16_t)len);
#endif
    }
}

void vTraceUserPrint(const char* channel, const char* format, ...)
{
    char details[96];
    va_list args;
    va_start(args, format);
    vsnprintf(details, sizeof(details), format, args);
    va_end(args);

    vTraceRecordEvent(ulTraceGetMicroseconds(), "UserEvent", 0, channel ? channel : "LOG", 0, details);
}

