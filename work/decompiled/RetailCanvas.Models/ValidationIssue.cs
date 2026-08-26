using System;

namespace RetailCanvas.Models;

public sealed class ValidationIssue
{
	public IssueSeverity Severity { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Detail { get; set; } = string.Empty;

	public Guid? ElementId { get; set; }

	public string ElementName { get; set; } = string.Empty;
}
