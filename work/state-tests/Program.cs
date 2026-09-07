using RetailCanvas.Models;
using RetailCanvas.Services;

var service = new ProjectService();
var project = new ProjectModel();
project.Pages.Clear();
for (int i = 0; i < 4; i++)
{
    var page = PageModel.Create("A4", false);
    page.Name = $"Page {i + 1}";
    page.Elements.Add(new CanvasElementModel {
        Kind = ElementKind.Text, Text = $"Page {i + 1}", FontSizePt = 3,
        Rotation = -45, WidthMm = 0.05, HeightMm = 1,
        TextFrameTight = true, PreserveAspectRatio = true
    });
    page.Elements.Add(new CanvasElementModel {
        Kind = ElementKind.Image, PdfPageIndex = i, PdfSourcePath = "original.pdf",
        ImageDataBase64 = "aW1hZ2U=", ImagePreTrimDataBase64 = "b3JpZ2luYWw=",
        IsLocked = true, WidthMm = 30, HeightMm = 40
    });
    project.Pages.Add(page);
}
string snapshot = service.Serialize(project);
var restored = service.DeserializeSnapshot(snapshot);
Require(service.Serialize(restored) == snapshot, "History must restore exact state without migration");
Require(restored.Pages.Count == 4, "All four pages survive history restore");
for (int i = 0; i < 4; i++)
{
    Require(restored.Pages[i].Elements[0].FontSizePt == 3, "3pt text survives history");
    Require(restored.Pages[i].Elements[0].Rotation == -45, "Negative rotation survives history");
    Require(restored.Pages[i].Elements[1].PdfPageIndex == i, "PDF page indices survive history");
    Require(restored.Pages[i].Elements[1].IsLocked, "Lock survives history");
}
var loaded = service.Deserialize(snapshot);
Require(loaded.Pages.Count == 4, "All four pages survive file load");
Require(loaded.Pages[0].Elements[0].WidthMm >= 0.1, "File load still normalizes unsafe dimensions");
Require(loaded.Pages[3].Elements[1].PdfPageIndex == 3, "Fourth PDF page survives file load");
Require(loaded.Pages[3].Elements[1].ImagePreTrimDataBase64 == "b3JpZ2luYWw=", "Original image survives file load");
project.FileFormatVersion = ProjectService.CurrentFileFormatVersion + 1;
bool rejected = false;
try { service.Deserialize(service.Serialize(project)); }
catch (System.IO.InvalidDataException) { rejected = true; }
Require(rejected, "Future file formats are still rejected");
foreach (bool initial in new[] { false, true })
foreach (bool reverse in new[] { false, true })
{
    var active = new CanvasElementModel { IsLocked = initial };
    var other = new CanvasElementModel { IsLocked = initial };
    var outsideSelection = new CanvasElementModel { IsLocked = initial };
    var selection = reverse ? new[] { other, active } : new[] { active, other };
    bool target = !active.IsLocked;
    ElementLockService.Apply(ElementLockService.GetChanges(selection, target), target);
    Require(selection.All(x => x.IsLocked == target), "Lock target is independent of active element order");
    Require(outsideSelection.IsLocked == initial, "Unselected elements retain lock state");
    Require(ElementLockService.GetChanges(selection, target).Count == 0, "Repeated lock does not create an edit");
    other.IsLocked = initial;
    Require(ElementLockService.GetChanges(selection, target).Count == 1, "Mixed selection changes only differing locks");
}
Console.WriteLine("PASS: history serialization, file migration, four-page preservation, 3pt and image data");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
