using System.Collections.Generic;
using IdCardPrintShop.Models;

namespace IdCardPrintShop.Services
{
    public interface ILayoutEngine
    {
        LayoutPlan CalculateLayout(IReadOnlyList<CardRegion> cards, LayoutTemplate template, int copies);
        LayoutPlan CalculateCustomSizeLayout(IReadOnlyList<CustomLayoutItem> items, CustomSizeLayoutParameters parameters);
    }
}
