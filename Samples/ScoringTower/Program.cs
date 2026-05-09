/**
 * Copyright (C)2024-2026 Scott Velez
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
**/

using Microsoft.Extensions.Logging;
using SVappsLAB.iRacingTelemetrySDK;

namespace ScoringTower
{
    [RequiredTelemetryVars([
        TelemetryVar.CarIdxF2Time,
        TelemetryVar.CarIdxLapCompleted,
        TelemetryVar.CarIdxLapDistPct,
        TelemetryVar.CarIdxPosition,
        TelemetryVar.Lap,
        TelemetryVar.SessionLapsTotal,
        TelemetryVar.SessionNum
    ])]
    internal class Program
    {
        private const int TowerWidth = 32;
        private const int MaxRows = 20;
        private const string Reset = "\u001b[0m";
        private const string Dim = "\u001b[38;5;245m";
        private const string White = "\u001b[97m";
        private const string Green = "\u001b[38;5;46m";
        private const string SunocoYellow = "\u001b[38;5;226m";
        private const string HeaderBlue = "\u001b[48;5;18m";
        private const string Dark = "\u001b[48;5;235m";
        private const string RowA = "\u001b[48;5;234m";
        private const string RowB = "\u001b[48;5;236m";
        private const string ActiveMarker = "\u001b[48;5;40m";

        private static readonly object StateLock = new();
        private static TelemetrySessionInfo? _session;
        private static TelemetryData _telemetry;
        private static bool _hasTelemetry;

        public static async Task Main(string[] args)
        {
            var options = TowerOptions.Parse(args);
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddConsole();
            });

            var logger = loggerFactory.CreateLogger("ScoringTower");
            IBTOptions? ibtOptions = string.IsNullOrWhiteSpace(options.IbtPath)
                ? null
                : new IBTOptions(options.IbtPath, options.PlaybackSpeed);

            await using var client = TelemetryClient<TelemetryData>.Create(logger, ibtOptions);
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;
            Console.Clear();

            var subscriptionTask = client.SubscribeToAllStreams(
                onTelemetryUpdate: data =>
                {
                    lock (StateLock)
                    {
                        _telemetry = data;
                        _hasTelemetry = true;
                    }

                    return Task.CompletedTask;
                },
                onSessionInfoUpdate: session =>
                {
                    lock (StateLock)
                    {
                        _session = session;
                    }

                    return Task.CompletedTask;
                },
                onConnectStateChanged: state =>
                {
                    Console.Title = $"iRacing scoring tower - {state}";
                    return Task.CompletedTask;
                },
                onError: error =>
                {
                    Console.Error.WriteLine(error);
                    return Task.CompletedTask;
                },
                cancellationToken: cts.Token);

            var renderTask = RenderLoopAsync(options, cts.Token);
            var monitorTask = client.Monitor(cts.Token);

            await Task.WhenAny(monitorTask, subscriptionTask, renderTask);
            cts.Cancel();

            try
            {
                await Task.WhenAll(monitorTask, subscriptionTask, renderTask);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
            finally
            {
                Console.CursorVisible = true;
                Console.Write(Reset);
            }
        }

        private static async Task RenderLoopAsync(TowerOptions options, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Render(options);
                await Task.Delay(options.RefreshMilliseconds, cancellationToken);
            }
        }

        private static void Render(TowerOptions options)
        {
            TelemetrySessionInfo? session;
            TelemetryData telemetry;
            bool hasTelemetry;

            lock (StateLock)
            {
                session = _session;
                telemetry = _telemetry;
                hasTelemetry = _hasTelemetry;
            }

            var rows = BuildRows(session, telemetry, hasTelemetry)
                .Take(options.Rows)
                .ToArray();

            Console.SetCursorPosition(0, 0);
            WriteHeader(options, session, telemetry, hasTelemetry);

            if (rows.Length == 0)
            {
                WriteLine(Dark, "Waiting for session standings...".PadRight(TowerWidth));
            }
            else
            {
                for (var i = 0; i < rows.Length; i++)
                    WriteDriverRow(rows[i], i);
            }

            for (var i = rows.Length; i < options.Rows; i++)
                WriteLine(i % 2 == 0 ? RowA : RowB, new string(' ', TowerWidth));

            WriteLine(Dark, $" Ctrl+C exit  {DateTime.Now:HH:mm:ss}".PadRight(TowerWidth));
        }

        private static void WriteHeader(TowerOptions options, TelemetrySessionInfo? session, TelemetryData telemetry, bool hasTelemetry)
        {
            WriteLine(HeaderBlue, $"{Dim}FUELED BY {SunocoYellow}SUNOCO{Reset}{HeaderBlue}".PadAnsiRight(TowerWidth));

            var title = options.Title
                ?? session?.WeekendInfo?.TrackDisplayShortName
                ?? session?.WeekendInfo?.TrackDisplayName
                ?? "IRACING SCORING";
            var sessionName = GetCurrentSession(session)?.SessionName ?? GetCurrentSession(session)?.SessionType ?? "SESSION";
            var lapText = FormatLapText(session, telemetry, hasTelemetry);

            WriteLine(Dark, $" {Truncate(title.ToUpperInvariant(), 21),-21} {Green}●{Reset}{Dark}".PadAnsiRight(TowerWidth));
            WriteLine(Dark, $" {Truncate(sessionName.ToUpperInvariant(), 13),-13} {White}{lapText,10}{Reset}{Dark}".PadAnsiRight(TowerWidth));
        }

        private static string FormatLapText(TelemetrySessionInfo? session, TelemetryData telemetry, bool hasTelemetry)
        {
            var total = hasTelemetry && telemetry.SessionLapsTotal is > 0 ? telemetry.SessionLapsTotal.Value : (int?)null;
            var current = hasTelemetry && telemetry.Lap is >= 0 ? telemetry.Lap.Value + 1 : (int?)null;

            if (total is null)
            {
                var sessionLaps = GetCurrentSession(session)?.SessionLaps;
                if (int.TryParse(sessionLaps, out var parsedTotal) && parsedTotal > 0)
                    total = parsedTotal;
            }

            return total is > 0 && current is > 0 ? $"{current} | {total}" : "-- | --";
        }

        private static IReadOnlyList<TowerRow> BuildRows(TelemetrySessionInfo? session, TelemetryData telemetry, bool hasTelemetry)
        {
            var drivers = session?.DriverInfo?.Drivers;
            if (drivers is null || drivers.Count == 0)
                return Array.Empty<TowerRow>();

            var resultPositions = GetCurrentSession(session)?.ResultsPositions?
                .Where(r => r.Position > 0)
                .ToDictionary(r => r.CarIdx, r => r);

            var rows = new List<TowerRow>();
            foreach (var driver in drivers.Where(d => d.CarIsPaceCar == 0 && d.IsSpectator == 0))
            {
                var carIdx = driver.CarIdx;
                var position = GetArrayValue(telemetry.CarIdxPosition, carIdx);
                if ((!hasTelemetry || position <= 0) && resultPositions?.TryGetValue(carIdx, out var result) == true)
                    position = result.Position;

                if (position <= 0)
                    continue;

                var lapCompleted = GetArrayValue(telemetry.CarIdxLapCompleted, carIdx);
                var lapPct = GetArrayValue(telemetry.CarIdxLapDistPct, carIdx);
                var gap = GetArrayValue(telemetry.CarIdxF2Time, carIdx);
                var displayName = !string.IsNullOrWhiteSpace(driver.AbbrevName)
                    ? driver.AbbrevName
                    : AbbreviateName(driver.UserName);

                rows.Add(new TowerRow(
                    Position: position,
                    CarNumber: string.IsNullOrWhiteSpace(driver.CarNumber) ? driver.CarNumberRaw.ToString() : driver.CarNumber,
                    DriverName: displayName,
                    Gap: gap,
                    LapsCompleted: lapCompleted,
                    LapDistancePct: lapPct));
            }

            return rows
                .OrderBy(r => r.Position)
                .ThenByDescending(r => r.LapsCompleted)
                .ThenByDescending(r => r.LapDistancePct)
                .ToArray();
        }

        private static void WriteDriverRow(TowerRow row, int rowIndex)
        {
            var background = rowIndex % 2 == 0 ? RowA : RowB;
            var pos = row.Position.ToString().PadLeft(2);
            var number = Truncate(row.CarNumber, 3).PadLeft(3);
            var name = Truncate(row.DriverName, 12).PadRight(12);
            var gap = row.Position == 1 ? string.Empty : FormatGap(row.Gap);
            var marker = row.Position <= 5 ? ActiveMarker : background;

            Console.Write(background);
            Console.Write($"{Dim}{pos}{Reset}{marker} ");
            Console.Write(background);
            Console.Write($"{SunocoYellow}{number}{Reset}{background} {White}{name}{Reset}{background}{Dim}{gap,7}{Reset}");
            Console.Write(new string(' ', Math.Max(0, TowerWidth - 28)));
            Console.WriteLine(Reset);
        }

        private static Session? GetCurrentSession(TelemetrySessionInfo? session)
        {
            var sessions = session?.SessionInfo?.Sessions;
            if (sessions is null || sessions.Count == 0)
                return null;

            var currentSessionNum = session?.SessionInfo?.CurrentSessionNum;
            return sessions.FirstOrDefault(s => s.SessionNum == currentSessionNum) ?? sessions.LastOrDefault();
        }

        private static int GetArrayValue(int[]? values, int index)
            => values is not null && index >= 0 && index < values.Length ? values[index] : 0;

        private static float GetArrayValue(float[]? values, int index)
            => values is not null && index >= 0 && index < values.Length ? values[index] : 0;

        private static string FormatGap(float gap)
        {
            if (gap <= 0 || float.IsNaN(gap) || float.IsInfinity(gap))
                return "";

            return gap >= 60 ? $"-{gap / 60:F1}m" : $"-{gap:F2}";
        }

        private static string AbbreviateName(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return "Unknown";

            var parts = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length switch
            {
                0 => "Unknown",
                1 => parts[0],
                _ => $"{parts[0][0]}. {parts[^1]}"
            };
        }

        private static string Truncate(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength];

        private static void WriteLine(string style, string text)
            => Console.WriteLine($"{style}{text}{Reset}");

        private sealed record TowerRow(
            int Position,
            string CarNumber,
            string DriverName,
            float Gap,
            int LapsCompleted,
            float LapDistancePct);

        private sealed record TowerOptions(string? IbtPath, string? Title, int Rows, int RefreshMilliseconds, int PlaybackSpeed)
        {
            public static TowerOptions Parse(string[] args)
            {
                string? ibtPath = null;
                string? title = null;
                var rows = MaxRows;
                var refreshMilliseconds = 500;
                var playbackSpeed = 1;

                for (var i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--title" when i + 1 < args.Length:
                            title = args[++i];
                            break;
                        case "--rows" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedRows):
                            rows = Math.Clamp(parsedRows, 1, MaxRows);
                            break;
                        case "--refresh" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedRefresh):
                            refreshMilliseconds = Math.Clamp(parsedRefresh, 100, 5_000);
                            break;
                        case "--speed" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedSpeed):
                            playbackSpeed = Math.Max(1, parsedSpeed);
                            break;
                        default:
                            ibtPath ??= args[i];
                            break;
                    }
                }

                return new TowerOptions(ibtPath, title, rows, refreshMilliseconds, playbackSpeed);
            }
        }
    }

    internal static class AnsiStringExtensions
    {
        public static string PadAnsiRight(this string value, int totalWidth)
        {
            var visible = 0;
            var inEscape = false;
            foreach (var c in value)
            {
                if (c == '\u001b')
                {
                    inEscape = true;
                    continue;
                }

                if (inEscape)
                {
                    if (c == 'm')
                        inEscape = false;
                    continue;
                }

                visible++;
            }

            return visible >= totalWidth ? value : value + new string(' ', totalWidth - visible);
        }
    }
}
