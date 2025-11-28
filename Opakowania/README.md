<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:converters="clr-namespace:Kalendarz1.Opakowania.Converters">

    <!-- ============================================== -->
    <!-- KOLORY - PALETA ZIELONO-CZERWONA -->
    <!-- ============================================== -->
    
    <!-- Kolory główne -->
    <SolidColorBrush x:Key="PrimaryGreenBrush" Color="#4B833C"/>
    <SolidColorBrush x:Key="PrimaryGreenHoverBrush" Color="#3A6D2E"/>
    <SolidColorBrush x:Key="PrimaryGreenLightBrush" Color="#E8F5E9"/>
    <SolidColorBrush x:Key="AccentRedBrush" Color="#CC2F37"/>
    <SolidColorBrush x:Key="AccentRedLightBrush" Color="#FEE2E2"/>
    <SolidColorBrush x:Key="AccentRedHoverBrush" Color="#B91C1C"/>
    
    <!-- Kolory pomocnicze -->
    <SolidColorBrush x:Key="BlueLightBrush" Color="#EBF5FF"/>
    <SolidColorBrush x:Key="BlueBorderBrush" Color="#3B82F6"/>
    <SolidColorBrush x:Key="OrangeLightBrush" Color="#FFF7ED"/>
    <SolidColorBrush x:Key="OrangeBorderBrush" Color="#F97316"/>
    <SolidColorBrush x:Key="PurpleLightBrush" Color="#F3E8FF"/>
    <SolidColorBrush x:Key="PurpleBorderBrush" Color="#A855F7"/>
    
    <!-- Kolory tekstu -->
    <SolidColorBrush x:Key="DarkTextBrush" Color="#2C3E50"/>
    <SolidColorBrush x:Key="MediumTextBrush" Color="#4B5563"/>
    <SolidColorBrush x:Key="LightTextBrush" Color="#7F8C8D"/>
    <SolidColorBrush x:Key="WhiteTextBrush" Color="#FFFFFF"/>
    
    <!-- Kolory tła -->
    <SolidColorBrush x:Key="BackgroundBrush" Color="#F3F4F6"/>
    <SolidColorBrush x:Key="CardBackgroundBrush" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="BorderBrush" Color="#D1D5DB"/>
    <SolidColorBrush x:Key="HoverBackgroundBrush" Color="#F9FAFB"/>
    
    <!-- Kolory statusów -->
    <SolidColorBrush x:Key="SuccessBrush" Color="#16A34A"/>
    <SolidColorBrush x:Key="SuccessLightBrush" Color="#DCFCE7"/>
    <SolidColorBrush x:Key="WarningBrush" Color="#F97316"/>
    <SolidColorBrush x:Key="WarningLightBrush" Color="#FEF3C7"/>
    <SolidColorBrush x:Key="ErrorBrush" Color="#DC2626"/>
    <SolidColorBrush x:Key="ErrorLightBrush" Color="#FEE2E2"/>
    
    <!-- ============================================== -->
    <!-- KONWERTERY -->
    <!-- ============================================== -->
    
    <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    <converters:SaldoToColorConverter x:Key="SaldoToColorConverter"/>
    <converters:SaldoToTextConverter x:Key="SaldoToTextConverter"/>
    <converters:PotwierdzenieToBackgroundConverter x:Key="PotwierdzenieToBackgroundConverter"/>
    <converters:StatusToColorConverter x:Key="StatusToColorConverter"/>
    <converters:NullToVisibilityConverter x:Key="NullToVisibilityConverter"/>
    <converters:DateToTextConverter x:Key="DateToTextConverter"/>
    <converters:InverseBoolConverter x:Key="InverseBoolConverter"/>
    <converters:SaldoCardBackgroundConverter x:Key="SaldoCardBackgroundConverter"/>
    <converters:BoolToFontWeightConverter x:Key="BoolToFontWeightConverter"/>
    <converters:ZeroToVisibilityConverter x:Key="ZeroToVisibilityConverter"/>
    <converters:BoolToWarningBackgroundConverter x:Key="BoolToWarningBackgroundConverter"/>
    <converters:StatusToBackgroundConverter x:Key="StatusToBackgroundConverter"/>

    <!-- ============================================== -->
    <!-- STYLE PRZYCISKÓW -->
    <!-- ============================================== -->
    
    <!-- Przycisk okna (minimize, maximize) -->
    <Style x:Key="WindowButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border" Background="{TemplateBinding Background}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#33FFFFFF"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Przycisk zamknięcia okna -->
    <Style x:Key="CloseButtonStyle" TargetType="Button" BasedOn="{StaticResource WindowButtonStyle}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border" Background="{TemplateBinding Background}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#E81123"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Przycisk podstawowy zielony -->
    <Style x:Key="PrimaryButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource PrimaryGreenBrush}"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Padding" Value="16,10"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border" Background="{TemplateBinding Background}" 
                            CornerRadius="6" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="{StaticResource PrimaryGreenHoverBrush}"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="border" Property="Background" Value="#D1D5DB"/>
                            <Setter Property="Foreground" Value="#9CA3AF"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Przycisk akcent czerwony -->
    <Style x:Key="AccentButtonStyle" TargetType="Button" BasedOn="{StaticResource PrimaryButtonStyle}">
        <Setter Property="Background" Value="{StaticResource AccentRedBrush}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border" Background="{TemplateBinding Background}" 
                            CornerRadius="6" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="{StaticResource AccentRedHoverBrush}"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="border" Property="Background" Value="#D1D5DB"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Przycisk pomocniczy -->
    <Style x:Key="SecondaryButtonStyle" TargetType="Button" BasedOn="{StaticResource PrimaryButtonStyle}">
        <Setter Property="Background" Value="#F3F4F6"/>
        <Setter Property="Foreground" Value="{StaticResource DarkTextBrush}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border" Background="{TemplateBinding Background}" 
                            CornerRadius="6" Padding="{TemplateBinding Padding}"
                            BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#E5E7EB"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Przycisk ikony opakowania -->
    <Style x:Key="PackageButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Width" Value="120"/>
        <Setter Property="Height" Value="80"/>
        <Setter Property="Margin" Value="5"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border" Background="{TemplateBinding Background}" 
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="8">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="{StaticResource PrimaryGreenLightBrush}"/>
                            <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource PrimaryGreenBrush}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE KARTY -->
    <!-- ============================================== -->
    
    <Style x:Key="CardStyle" TargetType="Border">
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="8"/>
        <Setter Property="Padding" Value="16"/>
    </Style>
    
    <Style x:Key="SaldoCardStyle" TargetType="Border">
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="8"/>
        <Setter Property="Padding" Value="12"/>
        <Setter Property="Margin" Value="4"/>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE TEXTBOX -->
    <!-- ============================================== -->
    
    <Style x:Key="ModernTextBoxStyle" TargetType="TextBox">
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10,8"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <Border x:Name="border" Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="6">
                        <ScrollViewer x:Name="PART_ContentHost" Margin="0"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsFocused" Value="True">
                            <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource PrimaryGreenBrush}"/>
                            <Setter TargetName="border" Property="BorderThickness" Value="2"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE DATAGRID -->
    <!-- ============================================== -->
    
    <Style x:Key="ModernDataGridStyle" TargetType="DataGrid">
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="GridLinesVisibility" Value="Horizontal"/>
        <Setter Property="HorizontalGridLinesBrush" Value="#F3F4F6"/>
        <Setter Property="RowBackground" Value="White"/>
        <Setter Property="AlternatingRowBackground" Value="#F9FAFB"/>
        <Setter Property="HeadersVisibility" Value="Column"/>
        <Setter Property="SelectionMode" Value="Single"/>
        <Setter Property="SelectionUnit" Value="FullRow"/>
        <Setter Property="CanUserAddRows" Value="False"/>
        <Setter Property="CanUserDeleteRows" Value="False"/>
        <Setter Property="AutoGenerateColumns" Value="False"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="RowHeight" Value="44"/>
    </Style>

    <Style x:Key="ModernDataGridColumnHeaderStyle" TargetType="DataGridColumnHeader">
        <Setter Property="Background" Value="#F3F4F6"/>
        <Setter Property="Foreground" Value="{StaticResource MediumTextBrush}"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="Padding" Value="12,10"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="0,0,0,1"/>
    </Style>

    <Style x:Key="ModernDataGridCellStyle" TargetType="DataGridCell">
        <Setter Property="Padding" Value="12,0"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="DataGridCell">
                    <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
                        <ContentPresenter VerticalAlignment="Center"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="{StaticResource PrimaryGreenLightBrush}"/>
                <Setter Property="Foreground" Value="{StaticResource DarkTextBrush}"/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="ModernDataGridRowStyle" TargetType="DataGridRow">
        <Setter Property="Background" Value="Transparent"/>
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="{StaticResource PrimaryGreenLightBrush}"/>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#F9FAFB"/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE COMBOBOX -->
    <!-- ============================================== -->

    <Style x:Key="ModernComboBoxStyle" TargetType="ComboBox">
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10,8"/>
        <Setter Property="FontSize" Value="13"/>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE DATEPICKER -->
    <!-- ============================================== -->

    <Style x:Key="ModernDatePickerStyle" TargetType="DatePicker">
        <Setter Property="Background" Value="White"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="8"/>
        <Setter Property="FontSize" Value="13"/>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE CHECKBOX -->
    <!-- ============================================== -->

    <Style x:Key="ModernCheckBoxStyle" TargetType="CheckBox">
        <Setter Property="Foreground" Value="{StaticResource DarkTextBrush}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Cursor" Value="Hand"/>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE LABEL/TEXTBLOCK -->
    <!-- ============================================== -->

    <Style x:Key="HeaderTextStyle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="20"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="Foreground" Value="{StaticResource DarkTextBrush}"/>
    </Style>

    <Style x:Key="SubHeaderTextStyle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Foreground" Value="{StaticResource MediumTextBrush}"/>
    </Style>

    <Style x:Key="LabelTextStyle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="Foreground" Value="{StaticResource LightTextBrush}"/>
        <Setter Property="Margin" Value="0,0,0,4"/>
    </Style>

    <Style x:Key="ValueTextStyle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="18"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="Foreground" Value="{StaticResource DarkTextBrush}"/>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE BADGE/TAG -->
    <!-- ============================================== -->

    <Style x:Key="SuccessBadgeStyle" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource SuccessLightBrush}"/>
        <Setter Property="CornerRadius" Value="10"/>
        <Setter Property="Padding" Value="8,4"/>
    </Style>

    <Style x:Key="WarningBadgeStyle" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource WarningLightBrush}"/>
        <Setter Property="CornerRadius" Value="10"/>
        <Setter Property="Padding" Value="8,4"/>
    </Style>

    <Style x:Key="ErrorBadgeStyle" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource ErrorLightBrush}"/>
        <Setter Property="CornerRadius" Value="10"/>
        <Setter Property="Padding" Value="8,4"/>
    </Style>

    <!-- ============================================== -->
    <!-- STYLE LOADING OVERLAY -->
    <!-- ============================================== -->

    <Style x:Key="LoadingOverlayStyle" TargetType="Grid">
        <Setter Property="Background" Value="#80FFFFFF"/>
        <Setter Property="Visibility" Value="Collapsed"/>
    </Style>

</ResourceDictionary>
