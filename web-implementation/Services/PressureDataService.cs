using System.ComponentModel;
using System.Diagnostics.Metrics;
using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GrapheneTrace.Web.Services;

public class PressureDataService
{
    // Author: 2414111
    // Add ApplicationDbContext to the service
    private readonly ApplicationDbContext applicationDbContext;
    public PressureDataService(ApplicationDbContext context)
    {
        applicationDbContext = context;
    }

    // Author 2414111
    // Set the number of pixels on the sensor
    private const int snapshotRows = 32;
    private const int snapshotColumns = 32;

    // Author: 2414111
    // Find the csv files, read the contents, split into groups of rows x columns ints
    public async Task ProcessInitialPressureData()
    {
        // Author: SID:2412494
        // Find the csv files in the Resources/GTLB-Data directory (moved to project root)
        string[] files = Directory.GetFiles("../Resources/GTLB-Data", "*.csv");

        // Process each file's contents
        foreach (string fileName in files)
        {
            // Split file path into separate bits of info
            char[] delimiterChar = ['\\', '/', '_', '.'];
            string[] fileNameSegments = fileName.Split(delimiterChar);

            // Author: SID:2412494
            // Path format: ../Resources/GTLB-Data/deviceId_date.csv
            // Split yields: [.., Resources, GTLB-Data, deviceId, date, csv] = 6 segments
            // deviceId at index 3, date at index 4
            if (fileNameSegments.Length != 6) continue;

            // Expect date to be at index 4 with format yyyyMMdd
            if (!DateTime.TryParseExact(fileNameSegments[4], "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime date)) continue;
            var parsedDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            // Don't duplicate entries in database by checking for existing deviceId and date
            if (await SessionAlreadyExists(fileNameSegments[3], parsedDate)) continue;

            string fileContents = ReadFile(fileName);

            // Save to the database and save the sessionId as a variable for adding to snapshot database entries later
            int sessionId = await SaveSessionToDB(fileNameSegments);

            // Split the file contents into strings of X rows for each snapshot
            List<string> sessionSnapshots = SplitIntoSnapshots(fileContents, snapshotRows);

            await SaveSnapshotToDB(sessionSnapshots, sessionId, parsedDate);
        }
        return;
    }

    // Author: 2414111
    // Check if there's a matching session in the database
    public async Task<bool> SessionAlreadyExists(string deviceId, DateTime parsedDate)
    {
        var session = await applicationDbContext.PatientSessionDatas
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.Start == parsedDate);
        return session != null;
    }

    // Author: 2414111
    // Save session to the database PatientSessionDatas table
    // Author: SID:2412494
    // Updated indices for new path format: deviceId at [3], date at [4]
    public async Task<int> SaveSessionToDB(string[] fileNameSegments)
    {
        var date = DateTime.ParseExact(fileNameSegments[4], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var dateUTC = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        var sessionData = new PatientSessionData
        {
            DeviceId = fileNameSegments[3],
            Start = dateUTC,
        };

        applicationDbContext.Add(sessionData);
        await applicationDbContext.SaveChangesAsync();

        //returning SessionID to add it when saving snapshots later
        return sessionData.SessionId;
    }

    // Author: 2414111
    // Save snapshot to the database PatientSnapshotDatas table
    // Author: SID:2412494
    // Updated to calculate and store PeakSnapshotPressure and CoefficientOfVariation
    public async Task SaveSnapshotToDB(List<string> sessionSnapshots, int sessionId, DateTime parsedDate)
    {
        // 15 snapshots per second
        double millisec = 1000.0 / 15.0;
        TimeSpan interval = TimeSpan.FromMilliseconds(millisec);

        // List for all the snapshots so they can be added at once rather than individually
        var snapshotsToAdd = new List<PatientSnapshotData>(sessionSnapshots.Count);

        foreach (string snapshot in sessionSnapshots)
        {
            // Change the string of data to a list of ints for calculating contact area %
            var snapshotInts = ConvertSnapshotToInt(snapshot, snapshotColumns);

            // Author: SID:2412494
            // Flatten to 1D array for PPI and CV calculations
            var flatArray = snapshotInts.SelectMany(row => row).ToArray();

            // Create an entry for a single snapshot
            var snapshotData = new PatientSnapshotData
            {
                SessionId = sessionId,
                SnapshotData = snapshot,
                ContactAreaPercent = SensorsOverLimitInSession(snapshotInts, 0) * (100.0f / 1024.0f),
                SnapshotTime = parsedDate,
                // Author: SID:2412494
                // Calculate and store Peak Pressure Index (excluding small clusters)
                PeakSnapshotPressure = CalculatePeakPressureIndex(flatArray),
                // Author: SID:2412494
                // Calculate and store Coefficient of Variation for pressure distribution analysis
                CoefficientOfVariation = CalculateCoefficientOfVariation(flatArray),
            };

            // Add snapshot to list
            snapshotsToAdd.Add(snapshotData);

            // Increase time for next snapshot
            parsedDate = parsedDate.Add(interval);
        }

        // Add all snapshots to database at once then save
        applicationDbContext.AddRange(snapshotsToAdd);
        await applicationDbContext.SaveChangesAsync();
        return;
    }

    // Author: 2414111
    // Function to read the file contents and return it as a string
    public static string ReadFile(string fileName)
    {
        try
        {
            // Open file
            using StreamReader streamReader = new(fileName);

            // Read the stream as a string
            return streamReader.ReadToEnd();
        }
        catch (IOException e)
        {
            // If any errors with input, return an empty string
            Console.WriteLine("The file couldn't be read: ");
            Console.WriteLine(e.Message);
            return string.Empty;
        }
    }

    // Author: 2414111
    // Split the file contents into strings with X lines, each representing a snapshot
    public static List<string> SplitIntoSnapshots(string fileText, int snapshotLines)
    {
        // New list of strings to hold all the snapshots from a session
        List<string> snapshots = new List<string>();

        using (var reader = new StringReader(fileText))
        {
            // New list of strings to keep the current snapshot set until X lines are reached
            List<string> tempSnapshot = new List<string>();
            // Nullable string for the line being currently processed
            string? currentLine;

            // While there are still lines with data in the file
            while ((currentLine = reader.ReadLine()) != null)
            {
                tempSnapshot.Add(currentLine);
                // If there are now X lines in the current snapshot it's complete
                if (tempSnapshot.Count == snapshotLines)
                {
                    // Make a string with all X lines, separated by new lines
                    string currentSnapshot = String.Join('\n', tempSnapshot);
                    // Add the snapshot that's just been created to the list of all snapshots from the file
                    snapshots.Add(currentSnapshot);
                    // Remove the strings from the temporarySnapshot to start on the adding lines to the next snapshot
                    tempSnapshot.Clear();
                }
            }
        }
        return snapshots;
    }

    // Author: 2414111
    // Convert one snapshot from a string to a list of arrays of integers
    public static List<int[]> ConvertSnapshotToInt(string snapshot, int snapshotColumns)
    {
        // New variable for snapshot in the form of ints
        var snapshotWithInts = new List<int[]>();

        using (var reader = new StringReader(snapshot))
        {
            for (string? currentLine = reader.ReadLine(); currentLine != null; currentLine = reader.ReadLine())
            {
                // Split around commas
                string[] individualIntsAsStrings = currentLine.Split(",");

                // Convert string to int and add ints to tempArray
                int[] individualInts = IndividualInts(individualIntsAsStrings, snapshotColumns);

                // Add 1D array to list of arrays
                snapshotWithInts.Add(individualInts);
            }
        }
        return snapshotWithInts;
    }

    // Author: 2414111
    // Function to convert a string to multiple ints (e.g. one line of a pressure reading to X numbers)
    public static int[] IndividualInts(string[] individualIntsAsStrings, int snapshotColumns)
    {
        int[] individualInts = new int[snapshotColumns];
        for (int i = 0; i < snapshotColumns; i++)
        {
            try
            {
                individualInts[i] = Convert.ToInt32(individualIntsAsStrings[i]);
            }
            catch (FormatException)
            {
                Console.WriteLine("Input for conversion from string to digits was not digits.");
            }
            catch (OverflowException)
            {
                Console.WriteLine("The number for conversion from text doesn't fit Int32.");
            }
        }
        return individualInts;
    }

    // Author: 2414111
    // Function for finding highest number in snapshot
    // Was used for summarising test data and may be useful later
    // TODO if not used later remove the function
    public static int GetBiggestNumber(List<int[]> snapshot)
    {
        int biggest = 0;
        foreach (int[] j in snapshot)
        {
            foreach (int i in j)
            {
                if (i > biggest)
                {
                    biggest = i;
                }
            }
        }
        return biggest;
    }

    // Author: 2414111
    // Function to count the number of sensors over a limit (e.g. max expected value) in snapshot
    // Was used to count sensors over 255 in single snapshot of test data
    // TODO if not used later remove
    public static int SensorsOverLimitInSession(List<int[]> snapshot, int limit)
    {
        int count = 0;
        foreach (int[] j in snapshot)
        {
            foreach (int i in j)
            {
                if (i > limit)
                {
                    count++;
                }
            }
        }
        return count;
    }

    // Author: 2414111
    // Function to count how many sensors over a limit across all sessions
    // Most useful for evaluating data for number of potential errors
    // TODO if not used later remove
    public static void AllSensorsOverLimit(List<List<int[]>> sessionSnapshotInts, int limit)
    {
        int counter = 0;
        int overLimit = 0;
        int biggest = 0;
        foreach (List<int[]> snapshot in sessionSnapshotInts)
        {
            counter++;
            overLimit += SensorsOverLimitInSession(snapshot, limit);
            int newBiggest = GetBiggestNumber(snapshot);
            if (newBiggest > biggest)
            {
                biggest = newBiggest;
            }
        }
        double percentOver = (overLimit / (counter * 32.0 * 32.0)) * 100.0;
        Console.WriteLine(overLimit + " over the 255 limit out of " + (counter * 32 * 32) + " (" + percentOver.ToString("n2") + "%) with the highest value being " + biggest);
    }

    // Author: 2414111
    // Retrieve a list of all sessions from the database
    // TODO remove if left unused
    public async Task<List<PatientSessionData>> LoadSessionData()
    {
        return await applicationDbContext.PatientSessionDatas.ToListAsync();
    }

    // Author: SID:2412494
    // Retrieve sessions for a specific patient, ordered by start date descending
    public async Task<List<PatientSessionData>> GetSessionsForPatientAsync(Guid patientId)
    {
        return await applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId)
            .OrderByDescending(s => s.Start)
            .ToListAsync();
    }

    // Author: 2414111
    // Find sessionId in the database for deviceId and date
    public async Task<int?> FindSessionId(string deviceId, DateTime start)
    {
        var session = await applicationDbContext.PatientSessionDatas
            .FirstOrDefaultAsync(a => a.DeviceId == deviceId && a.Start == start);
        return session?.SessionId;
    }

    // Author: 2414111
    // Retrieve a list of all snapshots from the database
    // TODO remove if left unused
    public async Task<List<PatientSnapshotData>> LoadSnapshotData()
    {
        return await applicationDbContext.PatientSnapshotDatas.ToListAsync();
    }

    // Author: 2414111
    // Find all snapshots from a session in the database
    public async Task<List<PatientSnapshotData>> FindSnapshotsInSession(int sessionId)
    {
        return await applicationDbContext.PatientSnapshotDatas
            .Where(a => a.SessionId == sessionId)
            .ToListAsync();
    }

    // Author: 2414111
    // Function to list all the timestamps from a session
    public async Task<List<DateTime>> ListTimestamps(int sessionId)
    {
        return await applicationDbContext.PatientSnapshotDatas
            .Where(s => s.SessionId == sessionId && s.SnapshotTime != null)
            .OrderBy(s => s.SnapshotId)
            .Select(s => s.SnapshotTime!.Value)
            .ToListAsync();
    }

    // Author: 2414111
    // Function to return ints for all sensors from a snapshot, based on the snapshot timestamp
    public async Task<List<int[]>> ReturnSnapshotDataFromTimestamp(int sessionId, DateTime timestamp)
    {
        var snapshot = await applicationDbContext.PatientSnapshotDatas
            .Where(s => s.SessionId == sessionId && s.SnapshotTime == timestamp)
            .SingleOrDefaultAsync();
        if (snapshot == null) return new List<int[]>();
        return ConvertSnapshotToInt(snapshot.SnapshotData, snapshotColumns);
    }

    // Author: SID:2412494
    // Get count of sessions for a patient within a date range
    public async Task<int> GetSessionCountAsync(Guid patientId, DateTime? since = null)
    {
        var query = applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId);

        if (since.HasValue)
        {
            query = query.Where(s => s.Start >= since.Value);
        }

        return await query.CountAsync();
    }

    // Author: SID:2412494
    // Get count of sessions flagged for clinician review
    public async Task<int> GetFlaggedSessionCountAsync(Guid patientId, DateTime? since = null)
    {
        var query = applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId && s.ClinicianFlag);

        if (since.HasValue)
        {
            query = query.Where(s => s.Start >= since.Value);
        }

        return await query.CountAsync();
    }

    // Author: SID:2412494
    // Flag a session for clinician review (used when alerts are triggered)
    public async Task FlagSessionForReviewAsync(int sessionId)
    {
        var session = await applicationDbContext.PatientSessionDatas
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session != null && !session.ClinicianFlag)
        {
            session.ClinicianFlag = true;
            await applicationDbContext.SaveChangesAsync();
        }
    }

    // Author: SID:2412494
    // Flag session by device ID and date (for live monitoring where session ID may not be known)
    public async Task FlagSessionForReviewAsync(string deviceId, DateTime sessionStart)
    {
        var session = await applicationDbContext.PatientSessionDatas
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.Start.Date == sessionStart.Date);

        if (session != null && !session.ClinicianFlag)
        {
            session.ClinicianFlag = true;
            await applicationDbContext.SaveChangesAsync();
        }
    }

    // Author: SID:2412494
    // Get count of snapshots with peak pressure above threshold for a patient
    public async Task<int> GetHighPressureAlertCountAsync(Guid patientId, int threshold, DateTime? since = null)
    {
        var sessionIds = await applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId)
            .Select(s => s.SessionId)
            .ToListAsync();

        if (!sessionIds.Any()) return 0;

        var query = applicationDbContext.PatientSnapshotDatas
            .Where(s => sessionIds.Contains(s.SessionId) && s.PeakSnapshotPressure >= threshold);

        if (since.HasValue)
        {
            query = query.Where(s => s.SnapshotTime >= since.Value);
        }

        return await query.CountAsync();
    }

    // Author: SID:2412494
    // Calculate Peak Pressure Index properly - excluding isolated high-pressure areas < 10 pixels
    // This uses flood-fill to identify connected regions and only considers clusters >= 10 pixels
    public static int CalculatePeakPressureIndex(int[] pressureData, int threshold = 50)
    {
        const int size = 32;
        const int minClusterSize = 10;

        if (pressureData.Length != size * size) return pressureData.Max();

        // Track visited cells
        var visited = new bool[size * size];
        int peakPressureIndex = 0;

        for (int i = 0; i < pressureData.Length; i++)
        {
            if (visited[i] || pressureData[i] <= threshold) continue;

            // Found an unvisited high-pressure cell, flood fill to find cluster
            var cluster = new List<int>();
            var stack = new Stack<int>();
            stack.Push(i);

            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                if (idx < 0 || idx >= pressureData.Length) continue;
                if (visited[idx]) continue;
                if (pressureData[idx] <= threshold) continue;

                visited[idx] = true;
                cluster.Add(idx);

                // Get row and column
                int row = idx / size;
                int col = idx % size;

                // Add 4-connected neighbors
                if (row > 0) stack.Push(idx - size);        // up
                if (row < size - 1) stack.Push(idx + size); // down
                if (col > 0) stack.Push(idx - 1);           // left
                if (col < size - 1) stack.Push(idx + 1);    // right
            }

            // Only consider clusters with 10+ pixels
            if (cluster.Count >= minClusterSize)
            {
                int clusterMax = cluster.Max(idx => pressureData[idx]);
                if (clusterMax > peakPressureIndex)
                {
                    peakPressureIndex = clusterMax;
                }
            }
        }

        return peakPressureIndex;
    }

    // Author: SID:2412494
    // Calculate Coefficient of Variation for a pressure frame
    // CV = (standard deviation / mean) * 100 - measures pressure distribution uniformity
    public static float CalculateCoefficientOfVariation(int[] pressureData)
    {
        if (pressureData.Length == 0) return 0;

        // Only consider active sensors (pressure > 0)
        var activeSensors = pressureData.Where(p => p > 0).ToArray();
        if (activeSensors.Length < 2) return 0;

        double mean = activeSensors.Average();
        if (mean == 0) return 0;

        double sumSquaredDiff = activeSensors.Sum(p => Math.Pow(p - mean, 2));
        double stdDev = Math.Sqrt(sumSquaredDiff / activeSensors.Length);

        return (float)((stdDev / mean) * 100);
    }

    // Author: SID:2412494
    // Data models for chart rendering
    public class PressureDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class SessionMetricSummary
    {
        public int SessionId { get; set; }
        public DateTime SessionStart { get; set; }
        public double AvgPeakPressure { get; set; }
        public double MaxPeakPressure { get; set; }
        public double AvgContactArea { get; set; }
        public double AvgCoeffOfVariation { get; set; }
        public int TotalFrames { get; set; }
        public int FramesAboveThreshold { get; set; }
        public TimeSpan TimeAboveThreshold { get; set; }
    }

    public class AreaPressureData
    {
        public double TopLeft { get; set; }
        public double TopRight { get; set; }
        public double BottomLeft { get; set; }
        public double BottomRight { get; set; }
    }

    public class WeeklyComparison
    {
        public string Period { get; set; } = string.Empty;
        public double AvgPeakPressure { get; set; }
        public double AvgContactArea { get; set; }
        public int SessionCount { get; set; }
        public TimeSpan TotalTimeAboveThreshold { get; set; }
    }

    // Author: SID:2412494
    // Get Peak Pressure Index data points for charting over a time period
    public async Task<List<PressureDataPoint>> GetPeakPressureOverTimeAsync(
        Guid patientId, DateTime startTime, DateTime endTime, int maxPoints = 100)
    {
        var sessionIds = await applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId && s.Start >= startTime && s.Start <= endTime)
            .Select(s => s.SessionId)
            .ToListAsync();

        if (!sessionIds.Any()) return new List<PressureDataPoint>();

        var snapshots = await applicationDbContext.PatientSnapshotDatas
            .Where(s => sessionIds.Contains(s.SessionId) &&
                        s.SnapshotTime >= startTime &&
                        s.SnapshotTime <= endTime)
            .OrderBy(s => s.SnapshotTime)
            .Select(s => new { s.SnapshotTime, s.PeakSnapshotPressure, s.SnapshotData })
            .ToListAsync();

        if (!snapshots.Any()) return new List<PressureDataPoint>();

        // Calculate PPI for snapshots that don't have it stored
        var dataPoints = new List<PressureDataPoint>();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.SnapshotTime == null) continue;

            int ppi;
            if (snapshot.PeakSnapshotPressure.HasValue)
            {
                ppi = snapshot.PeakSnapshotPressure.Value;
            }
            else
            {
                // Calculate PPI from snapshot data
                var pressureArray = ParseSnapshotToArray(snapshot.SnapshotData);
                ppi = CalculatePeakPressureIndex(pressureArray);
            }

            dataPoints.Add(new PressureDataPoint
            {
                Timestamp = snapshot.SnapshotTime.Value,
                Value = ppi
            });
        }

        // Downsample if too many points
        if (dataPoints.Count > maxPoints)
        {
            return DownsampleDataPoints(dataPoints, maxPoints);
        }

        return dataPoints;
    }

    // Author: SID:2412494
    // Get session-level metrics for time above threshold chart
    public async Task<List<SessionMetricSummary>> GetSessionMetricsAsync(
        Guid patientId, DateTime startTime, DateTime endTime, int threshold)
    {
        var sessions = await applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId && s.Start >= startTime && s.Start <= endTime)
            .OrderBy(s => s.Start)
            .ToListAsync();

        var summaries = new List<SessionMetricSummary>();
        const double frameIntervalMs = 1000.0 / 15.0; // 15 fps

        foreach (var session in sessions)
        {
            var snapshots = await applicationDbContext.PatientSnapshotDatas
                .Where(s => s.SessionId == session.SessionId)
                .ToListAsync();

            if (!snapshots.Any()) continue;

            // Calculate metrics for each snapshot
            var peakPressures = new List<int>();
            var contactAreas = new List<float>();
            var coefVariations = new List<float>();
            int framesAboveThreshold = 0;

            foreach (var snapshot in snapshots)
            {
                var pressureArray = ParseSnapshotToArray(snapshot.SnapshotData);
                int ppi = snapshot.PeakSnapshotPressure ?? CalculatePeakPressureIndex(pressureArray);

                peakPressures.Add(ppi);
                contactAreas.Add(snapshot.ContactAreaPercent ?? 0);

                // Calculate CV if not stored
                float cv = snapshot.CoefficientOfVariation ?? CalculateCoefficientOfVariation(pressureArray);
                coefVariations.Add(cv);

                if (ppi >= threshold) framesAboveThreshold++;
            }

            summaries.Add(new SessionMetricSummary
            {
                SessionId = session.SessionId,
                SessionStart = session.Start,
                AvgPeakPressure = peakPressures.Any() ? peakPressures.Average() : 0,
                MaxPeakPressure = peakPressures.Any() ? peakPressures.Max() : 0,
                AvgContactArea = contactAreas.Any() ? contactAreas.Average() : 0,
                AvgCoeffOfVariation = coefVariations.Any() ? coefVariations.Average() : 0,
                TotalFrames = snapshots.Count,
                FramesAboveThreshold = framesAboveThreshold,
                TimeAboveThreshold = TimeSpan.FromMilliseconds(framesAboveThreshold * frameIntervalMs)
            });
        }

        return summaries;
    }

    // Author: SID:2412494
    // Get average pressure by quadrant for area heat map
    public async Task<AreaPressureData> GetAreaPressureAveragesAsync(
        Guid patientId, DateTime startTime, DateTime endTime)
    {
        var sessionIds = await applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId && s.Start >= startTime && s.Start <= endTime)
            .Select(s => s.SessionId)
            .ToListAsync();

        if (!sessionIds.Any())
            return new AreaPressureData();

        var snapshots = await applicationDbContext.PatientSnapshotDatas
            .Where(s => sessionIds.Contains(s.SessionId) &&
                        s.SnapshotTime >= startTime &&
                        s.SnapshotTime <= endTime)
            .Select(s => s.SnapshotData)
            .ToListAsync();

        if (!snapshots.Any())
            return new AreaPressureData();

        double topLeftSum = 0, topRightSum = 0, bottomLeftSum = 0, bottomRightSum = 0;
        int count = 0;

        foreach (var snapshotData in snapshots)
        {
            var pressureArray = ParseSnapshotToArray(snapshotData);
            if (pressureArray.Length != 1024) continue; // 32x32

            // Calculate quadrant averages
            // Top-left: rows 0-15, cols 0-15
            // Top-right: rows 0-15, cols 16-31
            // Bottom-left: rows 16-31, cols 0-15
            // Bottom-right: rows 16-31, cols 16-31
            double tl = 0, tr = 0, bl = 0, br = 0;

            for (int row = 0; row < 32; row++)
            {
                for (int col = 0; col < 32; col++)
                {
                    int idx = row * 32 + col;
                    int val = pressureArray[idx];

                    if (row < 16 && col < 16) tl += val;
                    else if (row < 16 && col >= 16) tr += val;
                    else if (row >= 16 && col < 16) bl += val;
                    else br += val;
                }
            }

            // Each quadrant has 256 cells (16x16)
            topLeftSum += tl / 256;
            topRightSum += tr / 256;
            bottomLeftSum += bl / 256;
            bottomRightSum += br / 256;
            count++;
        }

        if (count == 0)
            return new AreaPressureData();

        return new AreaPressureData
        {
            TopLeft = topLeftSum / count,
            TopRight = topRightSum / count,
            BottomLeft = bottomLeftSum / count,
            BottomRight = bottomRightSum / count
        };
    }

    // Author: SID:2412494
    // Get weekly comparison data for trend analysis
    public async Task<List<WeeklyComparison>> GetWeeklyComparisonAsync(
        Guid patientId, int weeksBack, int threshold)
    {
        var comparisons = new List<WeeklyComparison>();
        var now = DateTime.UtcNow;
        const double frameIntervalMs = 1000.0 / 15.0;

        for (int w = 0; w < weeksBack; w++)
        {
            var weekEnd = now.AddDays(-7 * w);
            var weekStart = weekEnd.AddDays(-7);

            var sessions = await applicationDbContext.PatientSessionDatas
                .Where(s => s.PatientId == patientId && s.Start >= weekStart && s.Start < weekEnd)
                .ToListAsync();

            if (!sessions.Any())
            {
                comparisons.Add(new WeeklyComparison
                {
                    Period = GetWeekLabel(w),
                    SessionCount = 0
                });
                continue;
            }

            var sessionIds = sessions.Select(s => s.SessionId).ToList();
            var snapshots = await applicationDbContext.PatientSnapshotDatas
                .Where(s => sessionIds.Contains(s.SessionId))
                .ToListAsync();

            var peakPressures = new List<int>();
            var contactAreas = new List<float>();
            int framesAboveThreshold = 0;

            foreach (var snapshot in snapshots)
            {
                var pressureArray = ParseSnapshotToArray(snapshot.SnapshotData);
                int ppi = snapshot.PeakSnapshotPressure ?? CalculatePeakPressureIndex(pressureArray);

                peakPressures.Add(ppi);
                contactAreas.Add(snapshot.ContactAreaPercent ?? 0);
                if (ppi >= threshold) framesAboveThreshold++;
            }

            comparisons.Add(new WeeklyComparison
            {
                Period = GetWeekLabel(w),
                AvgPeakPressure = peakPressures.Any() ? peakPressures.Average() : 0,
                AvgContactArea = contactAreas.Any() ? contactAreas.Average() : 0,
                SessionCount = sessions.Count,
                TotalTimeAboveThreshold = TimeSpan.FromMilliseconds(framesAboveThreshold * frameIntervalMs)
            });
        }

        // Reverse so oldest week is first
        comparisons.Reverse();
        return comparisons;
    }

    // Author: SID:2412494
    // Get aggregated metrics for a specific time period (1h, 6h, 24h, custom)
    public async Task<SessionMetricSummary?> GetAggregatedMetricsAsync(
        Guid patientId, DateTime startTime, DateTime endTime, int threshold)
    {
        var sessionIds = await applicationDbContext.PatientSessionDatas
            .Where(s => s.PatientId == patientId && s.Start >= startTime && s.Start <= endTime)
            .Select(s => s.SessionId)
            .ToListAsync();

        if (!sessionIds.Any()) return null;

        var snapshots = await applicationDbContext.PatientSnapshotDatas
            .Where(s => sessionIds.Contains(s.SessionId) &&
                        s.SnapshotTime >= startTime &&
                        s.SnapshotTime <= endTime)
            .ToListAsync();

        if (!snapshots.Any()) return null;

        const double frameIntervalMs = 1000.0 / 15.0;
        var peakPressures = new List<int>();
        var contactAreas = new List<float>();
        var coefVariations = new List<float>();
        int framesAboveThreshold = 0;

        foreach (var snapshot in snapshots)
        {
            var pressureArray = ParseSnapshotToArray(snapshot.SnapshotData);
            int ppi = snapshot.PeakSnapshotPressure ?? CalculatePeakPressureIndex(pressureArray);

            peakPressures.Add(ppi);
            contactAreas.Add(snapshot.ContactAreaPercent ?? 0);

            float cv = snapshot.CoefficientOfVariation ?? CalculateCoefficientOfVariation(pressureArray);
            coefVariations.Add(cv);

            if (ppi >= threshold) framesAboveThreshold++;
        }

        return new SessionMetricSummary
        {
            SessionId = 0, // Aggregated
            SessionStart = startTime,
            AvgPeakPressure = peakPressures.Average(),
            MaxPeakPressure = peakPressures.Max(),
            AvgContactArea = contactAreas.Average(),
            AvgCoeffOfVariation = coefVariations.Average(),
            TotalFrames = snapshots.Count,
            FramesAboveThreshold = framesAboveThreshold,
            TimeAboveThreshold = TimeSpan.FromMilliseconds(framesAboveThreshold * frameIntervalMs)
        };
    }

    // Author: SID:2412494
    // Helper to parse snapshot string to int array
    private static int[] ParseSnapshotToArray(string snapshotData)
    {
        if (string.IsNullOrEmpty(snapshotData)) return Array.Empty<int>();

        var values = snapshotData
            .Replace("\n", ",")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out int v) ? v : 0)
            .ToArray();

        return values;
    }

    // Author: SID:2412494
    // Helper to downsample data points for performance
    private static List<PressureDataPoint> DownsampleDataPoints(
        List<PressureDataPoint> points, int targetCount)
    {
        if (points.Count <= targetCount) return points;

        var result = new List<PressureDataPoint>(targetCount);
        double step = (double)points.Count / targetCount;

        for (int i = 0; i < targetCount; i++)
        {
            int idx = (int)(i * step);
            result.Add(points[idx]);
        }

        return result;
    }

    // Author: SID:2412494
    // Helper to get week label (This week, Last week, 2 weeks ago, etc.)
    private static string GetWeekLabel(int weeksAgo)
    {
        return weeksAgo switch
        {
            0 => "This week",
            1 => "Last week",
            _ => $"{weeksAgo} weeks ago"
        };
    }
}