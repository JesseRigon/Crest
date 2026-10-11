namespace Crest.Workflows;

/// <summary>
/// Marks an activity whose effect leaves the database (an e-mail, an SMS, an HTTP call, a
/// notification): the engine runs it as a background activity after the unit of work that
/// reached it commits, in a unit of its own, and the flow resumes with its outcomes. A unit
/// that fails never runs it. Declared here so a module's activity can carry it without
/// referencing the engine module; the atomicity analyzer reads it to report where a flow
/// stops being atomic (docs/workflows.md › Units of work).
/// </summary>
public interface IUnitBoundary;
