using SshManage.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SshManage.Services;

public class ConnectionTestService
{
    public async Task<(bool Success, string Message, TimeSpan Duration)> TestConnectionAsync(
        string host, int port, int timeoutSeconds = 10, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            stopwatch.Stop();

            if (completedTask == timeoutTask)
            {
                return (false, "连接超时", stopwatch.Elapsed);
            }

            if (connectTask.IsCompletedSuccessfully)
            {
                return (true, $"连接成功，耗时 {stopwatch.ElapsedMilliseconds}ms", stopwatch.Elapsed);
            }
            else
            {
                var innerEx = connectTask.Exception?.InnerException ?? connectTask.Exception;
                return (false, $"连接失败: {innerEx?.Message ?? "未知错误"}", stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return (false, $"连接异常: {ex.Message}", stopwatch.Elapsed);
        }
    }

    public async Task<(bool Success, string Message, TimeSpan Duration)> TestSshConnectionAsync(
        string host, int port, string user, int timeoutSeconds = 15, CancellationToken cancellationToken = default)
    {
        var tcpResult = await TestConnectionAsync(host, port, timeoutSeconds, cancellationToken);
        if (!tcpResult.Success)
        {
            return tcpResult;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = $"-o ConnectTimeout={timeoutSeconds} -o BatchMode=yes -p {port} {user}@{host} exit",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "无法启动SSH进程", tcpResult.Duration);
            }

            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var waitTask = process.WaitForExitAsync(timeoutCts.Token);

            try
            {
                await waitTask;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { }
                return (false, "SSH连接超时", tcpResult.Duration);
            }

            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                return (true, "SSH连接测试成功", tcpResult.Duration);
            }
            else if (error.Contains("Permission denied"))
            {
                return (false, "权限验证失败，请检查密钥或密码", tcpResult.Duration);
            }
            else if (error.Contains("Host key verification failed"))
            {
                return (false, "主机密钥验证失败", tcpResult.Duration);
            }
            else
            {
                return (false, $"SSH连接失败: {error.Trim()}", tcpResult.Duration);
            }
        }
        catch (Exception ex)
        {
            return (false, $"SSH测试异常: {ex.Message}", tcpResult.Duration);
        }
    }

    public async Task<Dictionary<string, (bool Success, string Message, TimeSpan Duration)>> BatchTestAsync(
        IEnumerable<SshSite> sites, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, (bool, string, TimeSpan)>();
        var siteList = new List<SshSite>(sites);
        var completed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 5,
            CancellationToken = cancellationToken
        };

        await Task.Run(() =>
        {
            Parallel.ForEach(siteList, parallelOptions, site =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var result = TestConnectionAsync(
                    string.IsNullOrEmpty(site.HostName) ? site.Host : site.HostName,
                    site.Port,
                    10,
                    cancellationToken).GetAwaiter().GetResult();

                lock (results)
                {
                    results[site.Host] = result;
                    completed++;
                    progress?.Report(completed);
                }
            });
        }, cancellationToken);

        return results;
    }
}
