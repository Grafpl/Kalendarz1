using System;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.WPF.Controls
{
    public partial class DiffViewer : UserControl
    {
        public DiffViewer()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public static readonly DependencyProperty OldValueProperty =
            DependencyProperty.Register(nameof(OldValue), typeof(string), typeof(DiffViewer),
                new PropertyMetadata("(brak)"));

        public string OldValue
        {
            get => (string)GetValue(OldValueProperty);
            set => SetValue(OldValueProperty, value);
        }

        public static readonly DependencyProperty NewValueProperty =
            DependencyProperty.Register(nameof(NewValue), typeof(string), typeof(DiffViewer),
                new PropertyMetadata("(brak)"));

        public string NewValue
        {
            get => (string)GetValue(NewValueProperty);
            set => SetValue(NewValueProperty, value);
        }

        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DiffViewer),
                new PropertyMetadata(""));

        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public static readonly DependencyProperty ChangedByProperty =
            DependencyProperty.Register(nameof(ChangedBy), typeof(string), typeof(DiffViewer),
                new PropertyMetadata(""));

        public string ChangedBy
        {
            get => (string)GetValue(ChangedByProperty);
            set => SetValue(ChangedByProperty, value);
        }

        public static readonly DependencyProperty ChangeDateProperty =
            DependencyProperty.Register(nameof(ChangeDate), typeof(DateTime?), typeof(DiffViewer),
                new PropertyMetadata(null));

        public DateTime? ChangeDate
        {
            get => (DateTime?)GetValue(ChangeDateProperty);
            set => SetValue(ChangeDateProperty, value);
        }

        #endregion

        #region Public Methods

        public void SetDiff(DiffItem diff)
        {
            OldValue = string.IsNullOrEmpty(diff.OldValue) ? "(brak)" : diff.OldValue;
            NewValue = string.IsNullOrEmpty(diff.NewValue) ? "(brak)" : diff.NewValue;
            FieldName = diff.FieldName;
            ChangedBy = diff.ChangedBy;
            ChangeDate = diff.ChangeDate;
        }

        #endregion
    }

    #region Models

    public class DiffItem
    {
        public string FieldName { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string ChangedBy { get; set; } = "";
        public DateTime ChangeDate { get; set; }
        public int OrderId { get; set; }

        public DiffItem() { }

        public DiffItem(string fieldName, string oldValue, string newValue,
            string changedBy, DateTime changeDate, int orderId = 0)
        {
            FieldName = fieldName;
            OldValue = oldValue;
            NewValue = newValue;
            ChangedBy = changedBy;
            ChangeDate = changeDate;
            OrderId = orderId;
        }

        /// <summary>
        /// Parsuje opis zmiany i wyciąga stare/nowe wartości
        /// </summary>
        public static DiffItem ParseFromDescription(string description, string user, DateTime date, int orderId)
        {
            var diff = new DiffItem
            {
                ChangedBy = user,
                ChangeDate = date,
                OrderId = orderId
            };

            // Próba parsowania formatu: "Zmiana [Pole]: [Stara] → [Nowa]"
            if (description.Contains("→") || description.Contains("->"))
            {
                var arrow = description.Contains("→") ? "→" : "->";
                var parts = description.Split(new[] { arrow }, StringSplitOptions.None);

                if (parts.Length == 2)
                {
                    // Sprawdź czy jest prefiks z nazwą pola
                    var leftPart = parts[0].Trim();
                    var colonIndex = leftPart.LastIndexOf(':');

                    if (colonIndex > 0)
                    {
                        diff.FieldName = leftPart.Substring(0, colonIndex).Trim();
                        diff.OldValue = leftPart.Substring(colonIndex + 1).Trim();
                    }
                    else
                    {
                        diff.OldValue = leftPart;
                    }

                    diff.NewValue = parts[1].Trim();
                }
            }
            else if (description.Contains(":"))
            {
                var colonIndex = description.IndexOf(':');
                diff.FieldName = description.Substring(0, colonIndex).Trim();
                diff.NewValue = description.Substring(colonIndex + 1).Trim();
            }
            else
            {
                diff.FieldName = "Zmiana";
                diff.NewValue = description;
            }

            return diff;
        }
    }

    #endregion
}
