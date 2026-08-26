using System.Collections.Generic;

namespace RetailCanvas.Services;

public sealed record CsvReadResult(List<List<string>> Rows, string EncodingName);
