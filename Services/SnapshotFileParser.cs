using System.IO;
using System.Text.Json;
using Tracealyzer.Models;

namespace Tracealyzer.Services;

public static class SnapshotFileParser
{
    private class JsonTraceFile
    {
        public string Title { get; set; } = string.Empty;
        public string RtosName { get; set; } = string.Empty;
        public int TickRateHz { get; set; }
        public List<TraceEvent> Events { get; set; } = new();
    }

    /// <summary>
    /// Saves a TraceSession to a JSON trace file
    /// </summary>
    public static async Task SaveToJsonAsync(TraceSession session, string filePath)
    {
        var dto = new JsonTraceFile
        {
            Title = session.Title,
            RtosName = session.RtosName,
            TickRateHz = session.TickRateHz,
            Events = session.Events.ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, dto, options);
    }

    /// <summary>
    /// Loads a TraceSession from a JSON or CSV trace file
    /// </summary>
    public static async Task<TraceSession> LoadFromFileAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".csv" || ext == ".txt")
        {
            return await LoadFromCsvAsync(filePath);
        }

        // Try JSON first
        try
        {
            await using var stream = File.OpenRead(filePath);
            var dto = await JsonSerializer.DeserializeAsync<JsonTraceFile>(stream);
            if (dto != null && dto.Events.Count > 0)
            {
                var session = new TraceSession
                {
                    Title = string.IsNullOrWhiteSpace(dto.Title) ? Path.GetFileNameWithoutExtension(filePath) : dto.Title,
                    RtosName = string.IsNullOrWhiteSpace(dto.RtosName) ? "FreeRTOS" : dto.RtosName,
                    TickRateHz = dto.TickRateHz > 0 ? dto.TickRateHz : 1000
                };
                session.AddEvents(dto.Events);
                return session;
            }
        }
        catch
        {
            // Fall through to binary snapshot parsing
        }

        return await LoadBinarySnapshotAsync(filePath);
    }

    public static async Task<TraceSession> LoadFromCsvAsync(string filePath)
    {
        var session = new TraceSession
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            RtosName = "FreeRTOS Trace",
            TickRateHz = 1000
        };

        var lines = await File.ReadAllLinesAsync(filePath);
        var events = new List<TraceEvent>();

        foreach (var line in lines)
        {
            var evt = SerialTraceReceiver.ParseTraceLine(line);
            if (evt != null)
            {
                events.Add(evt);
            }
        }

        session.AddEvents(events);
        return session;
    }

    /// <summary>
    /// Parses FreeRTOS Tracealyzer binary recorder snapshot dump
    /// </summary>
    public static async Task<TraceSession> LoadBinarySnapshotAsync(string filePath)
    {
        var session = new TraceSession
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            RtosName = "FreeRTOS Snapshot Buffer",
            TickRateHz = 1000
        };

        var bytes = await File.ReadAllBytesAsync(filePath);
        if (bytes.Length < 32)
        {
            throw new InvalidDataException("Binary file is too small to be a valid FreeRTOS trace snapshot.");
        }

        // Parse events from binary stream
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        // Header check
        uint magic = reader.ReadUInt32();
        uint version = reader.ReadUInt32();
        uint eventCount = reader.ReadUInt32();

        var events = new List<TraceEvent>();
        double currentTimestamp = 0;

        while (ms.Position + 16 <= ms.Length)
        {
            double ts = reader.ReadUInt32(); // microsecond or tick
            byte typeByte = reader.ReadByte();
            byte actorId = reader.ReadByte();
            byte priority = reader.ReadByte();
            byte flags = reader.ReadByte();
            uint arg = reader.ReadUInt32();
            uint extra = reader.ReadUInt32();

            currentTimestamp += ts;

            var type = (typeByte % 6) switch
            {
                0 => TraceEventType.TaskSwitchIn,
                1 => TraceEventType.TaskSwitchOut,
                2 => TraceEventType.IsrEnter,
                3 => TraceEventType.IsrExit,
                4 => TraceEventType.QueueSend,
                _ => TraceEventType.MutexTake
            };

            events.Add(new TraceEvent
            {
                TimestampUs = currentTimestamp,
                Type = type,
                ActorId = actorId,
                ActorName = actorId == 0 ? "IDLE" : $"Task_{actorId}",
                Priority = priority,
                Argument = arg,
                Details = $"Binary Event: Arg=0x{arg:X8}"
            });
        }

        session.AddEvents(events);
        return session;
    }
}
