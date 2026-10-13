using System.Collections.ObjectModel;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Desktop.ViewModels;

/// <summary>
/// Cadastro de usuários: lista, criação, edição e desativação.
/// </summary>
public sealed class UserManagementViewModel : ObservableObject
{
    private readonly UserService _userService;
    private readonly Action<string>? _notify;

    private Guid? _editingId;
    private string _name = string.Empty;
    private string _username = string.Empty;
    private string _pin = string.Empty;
    private UserRole _role = UserRole.Operator;
    private bool _isActive = true;
    private string _message = string.Empty;
    private bool _usernameEnabled = true;

    /// <summary>
    /// Prepara os papéis disponíveis e os comandos da tela de usuários.
    /// </summary>
    public UserManagementViewModel(UserService userService, Action<string>? notify = null)
    {
        _userService = userService;
        _notify = notify;

        RoleOptions =
        [
            UserRole.Operator,
            UserRole.Manager,
            UserRole.Administrator
        ];

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        NewCommand = new RelayCommand(_ => ResetForm());
        EditCommand = new RelayCommand(p => BeginEdit(p as UserDto));
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        DeactivateCommand = new RelayCommand(async p => await DeactivateAsync(p as UserDto));
    }

    public ObservableCollection<UserDto> Users { get; } = [];
    public IReadOnlyList<UserRole> RoleOptions { get; }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeactivateCommand { get; }

    public string EditorTitle => _editingId is null ? "Novo usuário" : "Editar usuário";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetProperty(ref _pin, value);
    }

    public UserRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool UsernameEnabled
    {
        get => _usernameEnabled;
        set => SetProperty(ref _usernameEnabled, value);
    }

    /// <summary>
    /// Carrega a lista de usuários e limpa o formulário.
    /// </summary>
    public async Task LoadAsync()
    {
        Users.Clear();
        foreach (var user in await _userService.GetUsersAsync())
            Users.Add(user);
        ResetForm();
        Message = "Usuários carregados.";
    }

    /// <summary>
    /// Volta o formulário para o modo de novo usuário.
    /// </summary>
    private void ResetForm()
    {
        _editingId = null;
        Name = string.Empty;
        Username = string.Empty;
        Pin = string.Empty;
        Role = UserRole.Operator;
        IsActive = true;
        UsernameEnabled = true;
        RaisePropertyChanged(nameof(EditorTitle));
        Message = "Preencha os dados do usuário.";
    }

    /// <summary>
    /// Preenche o formulário com o usuário selecionado. O login fica bloqueado e o PIN em branco mantém o atual.
    /// </summary>
    private void BeginEdit(UserDto? user)
    {
        if (user is null)
            return;

        _editingId = user.Id;
        Name = user.Name;
        Username = user.Username;
        Pin = string.Empty;
        Role = user.Role;
        IsActive = user.IsActive;
        UsernameEnabled = false;
        RaisePropertyChanged(nameof(EditorTitle));
        Message = $"Editando {user.Username}. Deixe o PIN em branco para manter.";
    }

    /// <summary>
    /// Cria ou atualiza o usuário conforme o formulário e recarrega a lista.
    /// </summary>
    private async Task SaveAsync()
    {
        try
        {
            var request = new SaveUserRequest(Name, Username, Role, string.IsNullOrWhiteSpace(Pin) ? null : Pin, IsActive);
            if (_editingId is null)
                await _userService.CreateAsync(request);
            else
                await _userService.UpdateAsync(_editingId.Value, request);

            await LoadAsync();
            Message = "Usuário salvo.";
            _notify?.Invoke(Message);
        }
        catch (DomainException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Erro: {ex.Message}";
        }
    }

    /// <summary>
    /// Desativa o usuário informado e recarrega a lista.
    /// </summary>
    private async Task DeactivateAsync(UserDto? user)
    {
        if (user is null)
            return;

        try
        {
            await _userService.DeactivateAsync(user.Id);
            await LoadAsync();
            Message = $"Usuário {user.Username} desativado.";
            _notify?.Invoke(Message);
        }
        catch (DomainException ex)
        {
            Message = ex.Message;
        }
    }
}
