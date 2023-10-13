// Metrics
// - Log-based metrics: thorough, complete sets of events.
// - Standard: pre-aggregated, use backend for better accuracy. For real time, sampling/filtering
//   - Sampling:
//      - Adaptive: adjust in certain limits; used in functions
//      - Fixed-rate: for syncing client and server data to investigations of related events.
//      - Ingestion sampling: discard data to stay in monthly limit

// Group custom events by same OperationId value

// Usage analysis
// - Users tool: Counts unique app users per browser/machine.
// - Sessions tool: Tracks feature usage per session; resets after 30min inactivity or 24hr use.
// - Events tool: Measures page views and custom events like clicks.
// - Funnels: For linear, step-by-step processes
// - User Flows: For understanding complex, branching user behavior
// - Cohorts: Things in common
// - Impact: Performance effects
// - Retention: Returning users

// Monitor an app (Instrumentation)
// - Auto: Through config, no app code. OpenCensus for tracking metrics across services and technologies
// - Manual: Coding against the Application Insights or OpenTelemetry API. Supports Azure AD and Complex Tracing (collect data that is not available in Application Insights)

// Availability test
// - URL ping test: Checks endpoint, measures performance, customizable. Uses public DNS.
// - Standard test: Single request, covers SSL, HTTP verbs, custom headers.
// - Custom TrackAvailability: For multi-request/authentication tests. Use TrackAvailability() in code editor.
// Create an alert that will notify you via email if the web app becomes unresponsive:
// Portal > Application Insights resource > Availability > Add Test option > Rules (Alerts) > set action group for availability alert > Configure notifications (email, SMS)

// Azure Monitor: Infrastructure and multi-resource; hybrid and multi-cloud environments.
// - Activity Log: subscription-level events
//   - Log Analytics: Kusto
//   - Azure Storage account: audit, static analysis, or backup
//   - Azure Event Hubs: external systems
// Application Insights: App-level monitoring, focuses on web apps/services.

using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

class ApplicationInsightsService
{
    void Telemetry()
    {
        TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
        configuration.InstrumentationKey = "your-instrumentation-key-here";
        var telemetry = new TelemetryClient(configuration);
        // This information is attached to all events that the instance sends.
        telemetry.Context.User.Id = "...";
        telemetry.Context.Device.Id = "...";

        // For multi-request/authentication tests.
        telemetry.TrackAvailability("testName", DateTimeOffset.Now, TimeSpan.FromSeconds(30), "runLocation", true);

        telemetry.TrackEvent("WinGame"); // Custom events

        telemetry.GetMetric("metricId"); // pre-aggregation; lowers cost; no sampling

        // Prefer GetMetric()
        telemetry.TrackMetric(new MetricTelemetry() { Name = "queueLength", Sum = 42.3 });

        telemetry.TrackPageView("GameReviewPage");

        // Send a "breadcrumb trail" to Application Insights
        // Lets you send longer data such as POST information.
        telemetry.TrackTrace("Some message", SeverityLevel.Warning);

        // Event log: use ILogger or a class inheriting EventSource.

        // Track the response times and success rates of calls to an external piece of code
        var success = false;
        var startTime = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        try { success = true; }
        catch (Exception ex)
        {
            // Send exceptions to Application Insights
            telemetry.TrackException(ex);

            // Log exceptions to a diagnostic trace listener (Trace.aspx).
            Trace.TraceError(ex.Message);
        }
        finally
        {
            timer.Stop();
            // Send data to Dependency Tracking in Application Insights
            telemetry.TrackDependency("DependencyType", "myDependency", "myCall", startTime, timer.Elapsed, success);
        }

        telemetry.Flush();


    }
}