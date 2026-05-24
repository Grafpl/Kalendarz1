using System.Windows;
using System.Windows.Controls;
using Kalendarz1.AnalitykaPelna.Models;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Wybiera szablon wiersza wodospadu: Poziom (stan masy) vs Strata (ubytek).
    /// </summary>
    public class WaterfallStepTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? PoziomTemplate { get; set; }
        public DataTemplate? StrataTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is WaterfallStep step)
            {
                return step.Typ == WaterfallStepTyp.Strata ? StrataTemplate : PoziomTemplate;
            }
            return base.SelectTemplate(item, container);
        }
    }
}
