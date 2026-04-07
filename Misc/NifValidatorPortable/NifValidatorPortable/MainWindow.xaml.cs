using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NifValidatorPortable;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<CountryOption> CountryOptions { get; } = [];
    public ObservableCollection<string> ResultChecks { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        LoadCountryOptions();
        DataContext = this;

        Loaded += (_, _) => AnimateEntrance();
        ResetResult();
        TaxIdTextBox.Focus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void ValidateButton_OnClick(object sender, RoutedEventArgs e)
    {
        ValidateCurrentValue();
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        TaxIdTextBox.Clear();
        CountryComboBox.SelectedIndex = 0;
        ResetResult();
        TaxIdTextBox.Focus();
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NormalizedText.Text) && NormalizedText.Text != "-")
        {
            Clipboard.SetText(NormalizedText.Text);
            MessageText.Text = "Valor normalizado copiado para a área de transferência.";
        }
    }

    private void ExampleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tagValue })
        {
            return;
        }

        var parts = tagValue.Split('|');
        TaxIdTextBox.Text = parts[0];

        var targetCountry = parts.Length > 1 ? parts[1] : string.Empty;
        SelectCountry(targetCountry);

        ValidateCurrentValue();
    }

    private void TaxIdTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ValidateCurrentValue();
        }
    }

    private void TaxIdTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderTextBlock.Visibility = string.IsNullOrWhiteSpace(TaxIdTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ValidateCurrentValue()
    {
        var forcedCountry = GetSelectedCountryCode();
        var result = TaxIdValidator.ValidateTaxId(TaxIdTextBox.Text, forcedCountry);

        ResultChecks.Clear();
        foreach (var check in result.Checks)
        {
            ResultChecks.Add(check);
        }

        HeadlineText.Text = result.IsValid ? "Resultado válido" : "Resultado inválido";
        SummaryText.Text = result.Summary;
        MessageText.Text = result.IsValid
            ? "Estrutura aceite pelas regras disponíveis nesta app."
            : result.Error;

        NormalizedText.Text = string.IsNullOrWhiteSpace(result.Normalized) ? "-" : result.Normalized;
        CountryText.Text = string.IsNullOrWhiteSpace(result.CountryCode) ? "-" : result.CountryCode;
        TypeText.Text = string.IsNullOrWhiteSpace(result.Type) ? "-" : result.Type;
        ModeText.Text = string.IsNullOrWhiteSpace(result.ValidationMode) ? "-" : result.ValidationMode;
        CopyButton.IsEnabled = !string.IsNullOrWhiteSpace(result.Normalized);

        ApplyState(result.IsValid);
        AnimateResult();
        OnPropertyChanged(nameof(ResultChecks));
    }

    private void ResetResult()
    {
        ResultChecks.Clear();
        ResultChecks.Add("A aplicação vai listar aqui cada regra aplicada assim que fizeres a primeira validação.");

        HeadlineText.Text = "Insere um valor para validar";
        SummaryText.Text = "A resposta vai aparecer aqui com o resultado principal e as verificações aplicadas.";
        MessageText.Text = "Sem resultado ainda.";
        NormalizedText.Text = "-";
        CountryText.Text = "-";
        TypeText.Text = "-";
        ModeText.Text = "-";
        CopyButton.IsEnabled = false;

        StatusBadgeText.Text = "À espera";
        StatusBadge.Background = (Brush)FindResource("AccentSoftBrush");
        StatusBadgeText.Foreground = (Brush)FindResource("AccentBrush");
        MessageBorder.Background = (Brush)FindResource("AccentSoftBrush");
    }

    private void ApplyState(bool isValid)
    {
        if (isValid)
        {
            StatusBadgeText.Text = "Válido";
            StatusBadge.Background = (Brush)FindResource("SuccessSoftBrush");
            StatusBadgeText.Foreground = (Brush)FindResource("SuccessBrush");
            MessageBorder.Background = (Brush)FindResource("SuccessSoftBrush");
            return;
        }

        StatusBadgeText.Text = "Inválido";
        StatusBadge.Background = (Brush)FindResource("WarningSoftBrush");
        StatusBadgeText.Foreground = (Brush)FindResource("WarningBrush");
        MessageBorder.Background = (Brush)FindResource("WarningSoftBrush");
    }

    private void SelectCountry(string? code)
    {
        var match = CountryOptions.FirstOrDefault(option =>
            string.Equals(option.Code, code ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        CountryComboBox.SelectedItem = match ?? CountryOptions.FirstOrDefault();
    }

    private string? GetSelectedCountryCode()
    {
        return CountryComboBox.SelectedValue as string;
    }

    private void LoadCountryOptions()
    {
        CountryOptions.Clear();
        CountryOptions.Add(new CountryOption(string.Empty, "Auto/PT por omissao"));

        foreach (var country in IsoCountryCatalog.Countries)
        {
            CountryOptions.Add(country);
        }

        OnPropertyChanged(nameof(CountryOptions));
    }

    private void AnimateEntrance()
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450));
        RootGrid.BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateResult()
    {
        var pulse = new DoubleAnimationUsingKeyFrames();
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.985, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        ResultScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        ResultScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
