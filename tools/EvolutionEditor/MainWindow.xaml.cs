using DigimonBot.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EvolutionEditor;

public partial class MainWindow : Window
{
    private ObservableCollection<DigimonDefinition> _digimons = new();
    private DigimonDefinition? _currentDigimon;
    private string? _currentFilePath;
    private bool _isDirty = false;

    public MainWindow()
    {
        InitializeComponent();
        InitializeComboBoxes();
        DigimonList.ItemsSource = _digimons;
        EvolutionGrid.ItemsSource = new ObservableCollection<EvolutionOption>();
        UpdateStatus();
    }

    private void InitializeComboBoxes()
    {
        CmbStage.ItemsSource = Enum.GetValues<DigimonStage>();
        CmbPersonality.ItemsSource = Enum.GetValues<DigimonPersonality>();
    }

    private void UpdateStatus()
    {
        var fileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "未保存";
        var dirtyMark = _isDirty ? " *" : "";
        StatusText.Text = $"文件: {fileName}{dirtyMark} | 数码宝贝数量: {_digimons.Count}";
        BottomStatus.Text = _currentDigimon != null ? $"编辑中: {_currentDigimon.Name}" : "就绪";
    }

    private void DigimonList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DigimonList.SelectedItem is DigimonDefinition digimon)
        {
            LoadDigimonToEditor(digimon);
        }
    }

    private void LoadDigimonToEditor(DigimonDefinition digimon)
    {
        _currentDigimon = digimon;
        EditorPanel.IsEnabled = true;

        TxtId.Text = digimon.Id;
        TxtName.Text = digimon.Name;
        CmbStage.SelectedItem = digimon.Stage;
        CmbPersonality.SelectedItem = digimon.Personality;
        TxtAppearance.Text = digimon.Appearance ?? "";
        TxtBasePrompt.Text = digimon.BasePrompt;

        var evoCollection = new ObservableCollection<EvolutionOption>(
            digimon.NextEvolutions ?? new List<EvolutionOption>());
        EvolutionGrid.ItemsSource = evoCollection;
        evoCollection.CollectionChanged += (s, e) => MarkDirty();

        UpdatePreview();
        UpdateStatus();
    }

    private void UpdatePreview()
    {
        if (_currentDigimon == null) return;

        // 从UI更新当前对象
        _currentDigimon.Id = TxtId.Text;
        _currentDigimon.Name = TxtName.Text;
        _currentDigimon.Stage = (DigimonStage)(CmbStage.SelectedItem ?? DigimonStage.Baby1);
        _currentDigimon.Personality = (DigimonPersonality)(CmbPersonality.SelectedItem ?? DigimonPersonality.Brave);
        _currentDigimon.Appearance = TxtAppearance.Text;
        _currentDigimon.BasePrompt = TxtBasePrompt.Text;
        _currentDigimon.NextEvolutions = ((ObservableCollection<EvolutionOption>)EvolutionGrid.ItemsSource).ToList();

        // 显示JSON预览
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        TxtPreview.Text = JsonSerializer.Serialize(_currentDigimon, options);
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateStatus();
    }

    #region 事件处理

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirty && MessageBox.Show("当前有未保存的更改，确定要新建吗？", "确认", 
            MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        _digimons.Clear();
        _currentFilePath = null;
        _isDirty = false;
        EditorPanel.IsEnabled = false;
        UpdateStatus();
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                var loaded = JsonSerializer.Deserialize<List<DigimonDefinition>>(json, options);

                if (loaded != null)
                {
                    _digimons.Clear();
                    foreach (var d in loaded)
                    {
                        _digimons.Add(d);
                    }
                    _currentFilePath = dialog.FileName;
                    _isDirty = false;
                    EditorPanel.IsEnabled = false;
                    UpdateStatus();
                    MessageBox.Show($"成功加载 {loaded.Count} 个数码宝贝", "打开成功");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDigimon != null)
        {
            UpdatePreview(); // 确保最新数据
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = _currentFilePath ?? "digimon_database.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() }
                };
                var json = JsonSerializer.Serialize(_digimons.ToList(), options);
                File.WriteAllText(dialog.FileName, json);
                _currentFilePath = dialog.FileName;
                _isDirty = false;
                UpdateStatus();
                MessageBox.Show("保存成功！", "保存");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var newDigimon = new DigimonDefinition
        {
            Id = $"new_digimon_{_digimons.Count + 1}",
            Name = "新数码宝贝",
            Stage = DigimonStage.Child,
            Personality = DigimonPersonality.Brave,
            BasePrompt = "请在此输入基础设定...",
            NextEvolutions = new List<EvolutionOption>()
        };

        _digimons.Add(newDigimon);
        DigimonList.SelectedItem = newDigimon;
        MarkDirty();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (DigimonList.SelectedItem is DigimonDefinition digimon)
        {
            if (MessageBox.Show($"确定要删除 {digimon.Name} 吗？", "确认删除", 
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _digimons.Remove(digimon);
                EditorPanel.IsEnabled = false;
                MarkDirty();
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLower();
        var view = CollectionViewSource.GetDefaultView(_digimons);
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            view.Filter = null;
        }
        else
        {
            view.Filter = obj =>
            {
                if (obj is DigimonDefinition d)
                {
                    return d.Name.ToLower().Contains(searchText) || 
                           d.Id.ToLower().Contains(searchText);
                }
                return false;
            };
        }
    }

    private void BtnAddEvo_Click(object sender, RoutedEventArgs e)
    {
        if (EvolutionGrid.ItemsSource is ObservableCollection<EvolutionOption> collection)
        {
            collection.Add(new EvolutionOption
            {
                TargetId = "target_id",
                MinTokens = 10000,
                Requirements = new EmotionValues(),
                Priority = 1,
                Description = "进化描述"
            });
        }
    }

    private void BtnRemoveEvo_Click(object sender, RoutedEventArgs e)
    {
        if (EvolutionGrid.SelectedItem is EvolutionOption evo &&
            EvolutionGrid.ItemsSource is ObservableCollection<EvolutionOption> collection)
        {
            collection.Remove(evo);
        }
    }

    // 文本变化时更新预览
    private void TxtId_TextChanged(object sender, TextChangedEventArgs e) => OnFieldChanged();
    private void TxtName_TextChanged(object sender, TextChangedEventArgs e) => OnFieldChanged();
    private void CmbStage_SelectionChanged(object sender, SelectionChangedEventArgs e) => OnFieldChanged();
    private void CmbPersonality_SelectionChanged(object sender, SelectionChangedEventArgs e) => OnFieldChanged();
    private void TxtAppearance_TextChanged(object sender, TextChangedEventArgs e) => OnFieldChanged();
    private void TxtBasePrompt_TextChanged(object sender, TextChangedEventArgs e) => OnFieldChanged();

    private void OnFieldChanged()
    {
        MarkDirty();
        UpdatePreview();
    }

    #endregion
}
