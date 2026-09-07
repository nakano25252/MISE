using RetailCanvas.Models;

namespace RetailCanvas.Services;

public static class ElementLockService
{
    public static List<CanvasElementModel> GetChanges(IEnumerable<CanvasElementModel> elements, bool locked)
        => elements.Where(element => element.IsLocked != locked).ToList();

    public static void Apply(IEnumerable<CanvasElementModel> elements, bool locked)
    {
        foreach (var element in elements) element.IsLocked = locked;
    }
}
