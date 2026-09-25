/**
 * @file trcRecorder.c
 * @brief FreeRTOS Trace Recorder Hook Implementation for STM32 / ARM Cortex-M
 */

#include "trcRecorder.h"
#include <stdio.h>
#include <stdarg.h>

/* Replace this with your hardware UART transmit function (e.g., HAL_UART_Transmit or SEGGER_RTT_Write) */
extern void vHardwareTraceWrite(const uint8_t* pData, uint16_t length);

void vTraceRecorderInit(void)
{
    // Enable DWT Cycle Counter on Cortex-M for microsecond precision timestamps
    // CoreDebug->DEMCR |= CoreDebug_DEMCR_TRCENA_Msk;
    // DWT->CTRL |= DWT_CTRL_CYCCNTENA_Msk;
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
        vHardwareTraceWrite((const uint8_t*)buffer, (uint16_t)len);
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
