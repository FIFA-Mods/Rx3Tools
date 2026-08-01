using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rx3Tools
{
    public partial class FloatSpinBox : UserControl
    {
        public FloatSpinBox()
        {
            InitializeComponent();
        }

        // Dependency Property for Value
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(FloatSpinBox), new PropertyMetadata(0.0));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        // Standard properties (you can make these DependencyProperties too if needed)
        public double Step { get; set; } = 0.001;
        public double Minimum { get; set; } = double.MinValue;
        public double Maximum { get; set; } = double.MaxValue;

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (Value + Step <= Maximum)
                Value = Math.Round(Value + Step, 4); // Rounding prevents floating point errors
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            if (Value - Step >= Minimum)
                Value = Math.Round(Value - Step, 4);
        }

        // Validate text input to only allow numbers, decimals, and negative signs
        private void TxtValue_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                e.Handled = true;
                return;
            }

            foreach (char c in e.Text)
            {
                bool isDigit = char.IsDigit(c);
                bool isDecimal = c == '.' || c == ',';
                bool isNegative = c == '-';

                if (!(isDigit || isDecimal || isNegative))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void TxtValue_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                bool valid = true;
                foreach (char c in text)
                {
                    if (!(char.IsDigit(c) || c == '.' || c == ',' || c == '-'))
                    {
                        valid = false;
                        break;
                    }
                }
                if (!valid) e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        // Ensure value updates safely if the user manually types a number
        private void TxtValue_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtValue.Text, out double result))
            {
                Value = Math.Max(Minimum, Math.Min(result, Maximum));
            }
            else
            {
                // Reset to valid value if they typed garbage
                TxtValue.Text = Value.ToString("F5");
            }
        }
    }
}