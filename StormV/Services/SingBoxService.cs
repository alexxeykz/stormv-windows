namespace StormV.Services;

/// <summary>
/// Запускает sing-box как дочерний процесс с нужным конфигом.
/// Поддерживает все 7 протоколов.
/// </summary>
public class SingBoxService
{
    private Process? _process;
    private readonly string _singBoxPath;
    private readonly string _configPath;
    private bool _intentionalStop;
    public const int MixedPort = 2080;
    public const int ClashApiPort = 9090;

    public event Action<string>? LogReceived;
    public event Action<bool, string>? StatusChanged; // isRunning, error

    public SingBoxService()
    {
        var dir = ConfigService.ConfigDir;
        _singBoxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sing-box.exe");
        _configPath = Path.Combine(dir, "sing-box-config.json");
        Directory.CreateDirectory(dir);
    }

    public bool IsRunning => _process is { HasExited: false };

    public async Task<(bool success, string error)> StartAsync(ServerConfig server)
    {
        _intentionalStop = false;
        if (IsRunning) Stop();

        Logger.Instance.Info("SingBox", $"Запуск: {server.DisplayName} [{server.Protocol}]");

        if (!File.Exists(_singBoxPath))
        {
            var err = "sing-box.exe не найден. Поместите его в папку с приложением.";
            Logger.Instance.Error("SingBox", err);
            return (false, err);
        }

        try
        {
            // Авто-режим (urltest): используем готовый SingboxConfig из подписки
            var config = server.IsAuto && !string.IsNullOrEmpty(server.SingboxConfig)
                ? server.SingboxConfig
                : BuildConfig(server);
            File.WriteAllText(_configPath, config);
            Logger.Instance.Debug("SingBox", $"Конфиг записан: {_configPath}");

            var configErr = await ValidateConfigAsync();
            if (configErr != null)
            {
                Logger.Instance.Error("SingBox", $"Ошибка конфига: {configErr}");
                return (false, $"Ошибка конфига:\n{configErr}");
            }

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _singBoxPath,
                    Arguments = $"run -c \"{_configPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                LogReceived?.Invoke(e.Data);
                Logger.Instance.Info("sing-box", e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                LogReceived?.Invoke(e.Data);
                Logger.Instance.Warning("sing-box", e.Data);
            };
            _process.Exited += (_, _) =>
            {
                if (_intentionalStop) return;
                Logger.Instance.Warning("SingBox", "Процесс sing-box завершился неожиданно");
                StatusChanged?.Invoke(false, "sing-box завершился неожиданно");
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            Logger.Instance.Info("SingBox", "Процесс запущен, ожидание инициализации...");
            await Task.Delay(2000);

            if (_process.HasExited)
            {
                var err = "sing-box завершился сразу после запуска. Проверьте конфиг.";
                Logger.Instance.Error("SingBox", err);
                return (false, err);
            }

            Logger.Instance.Info("SingBox", $"Подключено! Прокси на 127.0.0.1:{MixedPort}");
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("SingBox", $"Исключение: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public void Stop()
    {
        _intentionalStop = true;
        Logger.Instance.Info("SingBox", "Остановка sing-box...");
        try
        {
            _process?.Kill(entireProcessTree: true);
            Logger.Instance.Info("SingBox", "Процесс остановлен");
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning("SingBox", $"Ошибка при остановке: {ex.Message}");
        }
        finally
        {
            _process = null;
        }
    }

    // ─── Config validation ───────────────────────────────────────────────────

    private async Task<string?> ValidateConfigAsync()
    {
        using var check = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _singBoxPath,
                Arguments = $"check -c \"{_configPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        check.Start();
        var stdout = await check.StandardOutput.ReadToEndAsync();
        var stderr = await check.StandardError.ReadToEndAsync();
        await check.WaitForExitAsync();

        if (check.ExitCode == 0) return null;
        var msg = (stdout + " " + stderr).Trim();
        return string.IsNullOrEmpty(msg) ? "sing-box check: невалидный конфиг" : msg;
    }

    // ─── Config builder ──────────────────────────────────────────────────────

    private static string BuildConfig(ServerConfig s)
    {
        var customDomains = SettingsService.Load().ProxyDomains;
        return BuildConfigWithDomains(s, customDomains);
    }

    private static string BuildConfigWithDomains(ServerConfig s, List<string> customDomains)
    {
        var outbound = s.Protocol switch
        {
            Protocol.Vless => BuildVless(s),
            Protocol.Vmess => BuildVmess(s),
            Protocol.Shadowsocks => BuildShadowsocks(s),
            Protocol.Trojan => BuildTrojan(s),
            Protocol.Hysteria2 => BuildHysteria2(s),
            Protocol.Tuic => BuildTuic(s),
            Protocol.WireGuard => BuildWireGuard(s),
            _ => throw new NotSupportedException($"Protocol {s.Protocol} is not supported")
        };

        var rules = new List<object>
        {
            new
            {
                ip_cidr = new[] {
                    "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
                    "127.0.0.0/8", "169.254.0.0/16", "fc00::/7"
                },
                outbound = "direct"
            },
            new
            {
                domain_suffix = new[] { "telegram.org", "t.me", "telegram.me", "telesco.pe" },
                outbound = "proxy"
            },
            new
            {
                ip_cidr = new[] {
                    "91.108.0.0/16", "91.105.192.0/23",
                    "149.154.160.0/20", "185.76.151.0/24", "95.161.76.0/24"
                },
                outbound = "proxy"
            },
            new
            {
                domain_suffix = new[] {
                    "youtube.com", "youtu.be", "googlevideo.com",
                    "ytimg.com", "ggpht.com", "youtube-nocookie.com"
                },
                outbound = "proxy"
            },
            new
            {
                domain_suffix = new[] { "whatsapp.com", "whatsapp.net" },
                outbound = "proxy"
            },
            new
            {
                ip_cidr = new[] {
                    "31.13.24.0/21", "31.13.64.0/18", "31.13.96.0/19",
                    "157.240.0.0/17", "173.252.64.0/18",
                    "69.63.176.0/20", "66.220.144.0/20"
                },
                outbound = "proxy"
            }
        };

        if (customDomains.Count > 0)
            rules.Add(new { domain_suffix = customDomains.ToArray(), outbound = "proxy" });

        var config = new
        {
            log = new { level = "info", timestamp = true },
            inbounds = new[]
            {
                new
                {
                    type = "mixed",
                    tag = "mixed-in",
                    listen = "127.0.0.1",
                    listen_port = MixedPort
                }
            },
            outbounds = new object[]
            {
                outbound,
                new { type = "direct", tag = "direct" },
                new { type = "block", tag = "block" }
            },
            route = new
            {
                rules = rules.ToArray(),
                final = "direct"
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object BuildVless(ServerConfig s)
    {
        var tls = BuildTls(s);
        var transport = BuildTransport(s);

        return new
        {
            type = "vless",
            tag = "proxy",
            server = s.Host,
            server_port = s.Port,
            uuid = s.Uuid,
            flow = string.IsNullOrEmpty(s.Flow) ? null : s.Flow,
            tls,
            transport
        };
    }

    private static object BuildVmess(ServerConfig s)
    {
        var tls = s.Security == "tls" ? BuildTls(s) : null;
        var transport = BuildTransport(s);

        return new
        {
            type = "vmess",
            tag = "proxy",
            server = s.Host,
            server_port = s.Port,
            uuid = s.Uuid,
            security = "auto",
            alter_id = s.AlterId,
            tls,
            transport
        };
    }

    private static object BuildShadowsocks(ServerConfig s) => new
    {
        type = "shadowsocks",
        tag = "proxy",
        server = s.Host,
        server_port = s.Port,
        method = s.Method,
        password = s.Password
    };

    private static object BuildTrojan(ServerConfig s)
    {
        var tls = (object)new
        {
            enabled = true,
            server_name = string.IsNullOrEmpty(s.Sni) ? s.Host : s.Sni,
            insecure = s.SkipCertVerify
        };
        var transport = BuildTransport(s);
        return new
        {
            type = "trojan",
            tag = "proxy",
            server = s.Host,
            server_port = s.Port,
            password = s.Password,
            tls,
            transport
        };
    }

    private static object BuildHysteria2(ServerConfig s)
    {
        var obfs = string.IsNullOrEmpty(s.Obfs) ? null : (object)new
        {
            type = s.Obfs,
            password = s.ObfsPassword
        };

        return new
        {
            type = "hysteria2",
            tag = "proxy",
            server = s.Host,
            server_port = s.Port,
            password = s.Password,
            obfs,
            tls = new
            {
                enabled = true,
                server_name = string.IsNullOrEmpty(s.Sni) ? s.Host : s.Sni,
                insecure = s.SkipCertVerify
            }
        };
    }

    private static object BuildTuic(ServerConfig s) => new
    {
        type = "tuic",
        tag = "proxy",
        server = s.Host,
        server_port = s.Port,
        uuid = s.Uuid,
        password = s.Password,
        congestion_control = s.CongestionControl,
        tls = new
        {
            enabled = true,
            server_name = string.IsNullOrEmpty(s.Sni) ? s.Host : s.Sni,
            insecure = s.SkipCertVerify
        }
    };

    private static object BuildWireGuard(ServerConfig s)
    {
        var peers = new[]
        {
            new
            {
                server = s.Host,
                server_port = s.Port,
                public_key = s.PublicKey,
                pre_shared_key = string.IsNullOrEmpty(s.PresharedKey) ? null : s.PresharedKey,
                allowed_ips = s.AllowedIPs.ToArray()
            }
        };
        return new
        {
            type = "wireguard",
            tag = "proxy",
            private_key = s.PrivateKey,
            peers,
            local_address = new[] { s.LocalAddress },
            mtu = s.Mtu
        };
    }

    private static object? BuildTls(ServerConfig s)
    {
        if (s.Security == "none" || string.IsNullOrEmpty(s.Security)) return null;

        if (s.Security == "reality")
        {
            var fp = string.IsNullOrEmpty(s.Fingerprint) ? "chrome" : s.Fingerprint;
            return new
            {
                enabled = true,
                server_name = s.Sni,
                utls = new { enabled = true, fingerprint = fp },
                reality = new
                {
                    enabled = true,
                    public_key = s.RealityPublicKey,
                    short_id = s.RealityShortId
                }
            };
        }

        // TLS
        return new
        {
            enabled = true,
            server_name = string.IsNullOrEmpty(s.Sni) ? s.Host : s.Sni,
            utls = new { enabled = !string.IsNullOrEmpty(s.Fingerprint), fingerprint = s.Fingerprint },
            insecure = s.SkipCertVerify
        };
    }

    private static object? BuildTransport(ServerConfig s)
    {
        var wsHost = string.IsNullOrEmpty(s.Host2) ? null : s.Host2;

        return s.Network switch
        {
            "ws" => new
            {
                type = "ws",
                path = string.IsNullOrEmpty(s.Path) ? "/" : s.Path,
                headers = wsHost == null ? null : (object)new { Host = wsHost }
            },
            "grpc" => new { type = "grpc", service_name = s.Path },
            "http" => new
            {
                type = "http",
                path = string.IsNullOrEmpty(s.Path) ? "/" : s.Path,
                host = wsHost == null ? null : new[] { wsHost }
            },
            _ => null
        };
    }

}
