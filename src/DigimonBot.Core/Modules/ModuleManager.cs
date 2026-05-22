using Microsoft.Extensions.Logging;

namespace DigimonBot.Core.Modules;

/// <summary>
/// Manages module lifecycle including loading, unloading, and hot-reloading.
/// The ModuleManager is the central dispatcher that routes messages to registered modules.
/// </summary>
public class ModuleManager
{
    private readonly ILogger<ModuleManager> _logger;
    private readonly List<IModule> _modules = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private IModuleContext? _context;

    public ModuleManager(ILogger<ModuleManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get a read-only list of currently loaded modules.
    /// </summary>
    public IReadOnlyList<IModule> Modules
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _modules.ToList().AsReadOnly();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Set the module context (must be called before loading modules).
    /// </summary>
    public void SetContext(IModuleContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Register and initialize a module.
    /// </summary>
    public async Task LoadModuleAsync(IModule module)
    {
        if (_context == null)
            throw new InvalidOperationException("ModuleContext must be set before loading modules.");

        _lock.EnterWriteLock();
        try
        {
            // Remove existing module with same name
            var existing = _modules.FirstOrDefault(m => m.Name == module.Name);
            if (existing != null)
            {
                _logger.LogInformation("Unloading existing module '{Name}' for replacement", existing.Name);
                await existing.ShutdownAsync();
                _modules.Remove(existing);
            }

            await module.InitializeAsync(_context);
            _modules.Add(module);
            _logger.LogInformation("Module '{Name}' loaded successfully", module.Name);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Unload a module by name.
    /// </summary>
    public async Task<bool> UnloadModuleAsync(string name)
    {
        _lock.EnterWriteLock();
        try
        {
            var module = _modules.FirstOrDefault(m => m.Name == name);
            if (module == null)
            {
                _logger.LogWarning("Module '{Name}' not found for unload", name);
                return false;
            }

            await module.ShutdownAsync();
            _modules.Remove(module);
            _logger.LogInformation("Module '{Name}' unloaded", name);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Reload a module by name. The module is shutdown and re-initialized.
    /// </summary>
    public async Task<bool> ReloadModuleAsync(string name)
    {
        _lock.EnterWriteLock();
        try
        {
            var module = _modules.FirstOrDefault(m => m.Name == name);
            if (module == null)
            {
                _logger.LogWarning("Module '{Name}' not found for reload", name);
                return false;
            }

            if (_context == null)
                throw new InvalidOperationException("ModuleContext must be set before reloading modules.");

            _logger.LogInformation("Reloading module '{Name}'...", name);
            await module.ShutdownAsync();
            await module.InitializeAsync(_context);
            _logger.LogInformation("Module '{Name}' reloaded successfully", name);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Reload all modules.
    /// </summary>
    public async Task ReloadAllAsync()
    {
        _lock.EnterReadLock();
        List<IModule> modules;
        try
        {
            modules = _modules.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var module in modules)
        {
            await ReloadModuleAsync(module.Name);
        }
    }

    /// <summary>
    /// Dispatch a message to all modules in order until one handles it.
    /// </summary>
    public async Task<ModuleResult> DispatchMessageAsync(ModuleMessage message)
    {
        _lock.EnterReadLock();
        List<IModule> modules;
        try
        {
            modules = _modules.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var module in modules)
        {
            try
            {
                var result = await module.HandleMessageAsync(message);
                if (result.Handled)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in module '{Name}' handling message", module.Name);
            }
        }

        return ModuleResult.NotHandled();
    }

    /// <summary>
    /// Dispatch an event to all modules.
    /// </summary>
    public async Task DispatchEventAsync(ModuleEvent evt)
    {
        _lock.EnterReadLock();
        List<IModule> modules;
        try
        {
            modules = _modules.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var module in modules)
        {
            try
            {
                await module.HandleEventAsync(evt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in module '{Name}' handling event", module.Name);
            }
        }
    }

    /// <summary>
    /// Shutdown all modules.
    /// </summary>
    public async Task ShutdownAllAsync()
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var module in _modules)
            {
                try
                {
                    await module.ShutdownAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error shutting down module '{Name}'", module.Name);
                }
            }
            _modules.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
