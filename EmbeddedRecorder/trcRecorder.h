/**
 * @file trcRecorder.h
 * @brief FreeRTOS Trace Recorder Hook Definitions for Tracealyzer PC
 * 
 * Simply include this file in your FreeRTOSConfig.h and enable trace hooks:
 * #define configUSE_TRACE_FACILITY 1
 * #include "trcRecorder.h"
 */

#ifndef TRC_RECORDER_H
#define TRC_RECORDER_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>

/* Initialize trace recorder (UART or TCP output buffer) */
void vTraceRecorderInit(void);

/* Send formatted trace event over UART / RTT */
void vTraceRecordEvent(uint32_t ts_us, const char* event_type, uint32_t actor_id, const char* name, uint32_t prio, const char* details);

/* User Event / Printf logging */
void vTraceUserPrint(const char* channel, const char* format, ...);

/* FreeRTOS Trace Hooks mapping */
#define traceTASK_SWITCHED_IN() \
    vTraceRecordEvent(ulTraceGetMicroseconds(), "TaskSwitchIn", (uint32_t)pxCurrentTCB, pcTaskGetName(NULL), uxTaskPriorityGet(NULL), "Switched in")

#define traceTASK_SWITCHED_OUT() \
    vTraceRecordEvent(ulTraceGetMicroseconds(), "TaskSwitchOut", (uint32_t)pxCurrentTCB, pcTaskGetName(NULL), uxTaskPriorityGet(NULL), "Switched out")

#define traceMOVED_TASK_TO_READY_STATE(pxTCB) \
    vTraceRecordEvent(ulTraceGetMicroseconds(), "TaskReady", (uint32_t)pxTCB, pcTaskGetName(pxTCB), uxTaskPriorityGet(pxTCB), "Ready state")

#define traceQUEUE_SEND(pxQueue) \
    vTraceRecordEvent(ulTraceGetMicroseconds(), "QueueSend", (uint32_t)pxCurrentTCB, pcTaskGetName(NULL), uxTaskPriorityGet(NULL), "Queue Send")

#define traceQUEUE_RECEIVE(pxQueue) \
    vTraceRecordEvent(ulTraceGetMicroseconds(), "QueueReceive", (uint32_t)pxCurrentTCB, pcTaskGetName(NULL), uxTaskPriorityGet(NULL), "Queue Receive")

#define traceTASK_DELAY() \
    vTraceRecordEvent(ulTraceGetMicroseconds(), "TaskDelay", (uint32_t)pxCurrentTCB, pcTaskGetName(NULL), uxTaskPriorityGet(NULL), "Task Delayed")

/* Microsecond counter hook (e.g. DWT->CYCCNT / (SystemCoreClock / 1000000)) */
uint32_t ulTraceGetMicroseconds(void);

#ifdef __cplusplus
}
#endif

#endif /* TRC_RECORDER_H */
