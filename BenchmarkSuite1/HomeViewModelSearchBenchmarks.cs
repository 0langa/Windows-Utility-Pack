using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.ViewModels;
using Microsoft.VSDiagnostics;

namespace WindowsUtilityPack.Benchmarks;
[HideColumns("Job", "RatioSD", "Error")]
[CPUUsageDiagnoser]
public class HomeViewModelSearchBenchmarks
{
    private IReadOnlyList<ToolDefinition> _tools = [];
    [Params("storage", "password", "Developer", "zzz_nomatch")]
    public string Query { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup() => _tools = BuildCatalog();
    /// <summary>
    /// Replicates the filtering loop in HomeViewModel.UpdateSearchResults.
    /// Returns the matched list so the JIT cannot elide the work.
    /// </summary>
    [Benchmark(Baseline = true)]
    public List<ToolDefinition> FilterTools()
    {
        var query = Query;
        var results = new List<ToolDefinition>();
        foreach (var tool in _tools)
        {
            if (tool.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || tool.Description.Contains(query, StringComparison.OrdinalIgnoreCase) || tool.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(tool);
            }
        }

        return results;
    }

    // ── Realistic catalog matching App.xaml.cs registrations ─────────────────
    private static IReadOnlyList<ToolDefinition> BuildCatalog() => [new()
    {
        Key = "storage-master",
        Name = "Storage Master",
        Category = "System Utilities",
        Description = "Advanced storage analysis, cleanup, and optimization",
        Factory = () => null!
    }, new()
    {
        Key = "startup-manager",
        Name = "Startup Manager",
        Category = "System Utilities",
        Description = "Manage startup entries from user and machine locations",
        Factory = () => null!
    }, new()
    {
        Key = "system-info-dashboard",
        Name = "System Info Dashboard",
        Category = "System Utilities",
        Description = "Hardware, OS, and system information at a glance",
        Factory = () => null!
    }, new()
    {
        Key = "env-vars-editor",
        Name = "Environment Variables",
        Category = "System Utilities",
        Description = "View and edit user and machine environment variables",
        Factory = () => null!
    }, new()
    {
        Key = "hosts-file-editor",
        Name = "Hosts File Editor",
        Category = "System Utilities",
        Description = "Edit Windows hosts file with syntax highlighting",
        Factory = () => null!
    }, new()
    {
        Key = "bulk-file-renamer",
        Name = "Bulk File Renamer",
        Category = "File & Data Tools",
        Description = "Batch rename files using patterns and rules",
        Factory = () => null!
    }, new()
    {
        Key = "file-hash-calculator",
        Name = "File Hash Calculator",
        Category = "File & Data Tools",
        Description = "Compute MD5, SHA-1, SHA-256, and SHA-512 hashes",
        Factory = () => null!
    }, new()
    {
        Key = "file-splitter-joiner",
        Name = "File Splitter & Joiner",
        Category = "File & Data Tools",
        Description = "Split large files into parts and rejoin them",
        Factory = () => null!
    }, new()
    {
        Key = "metadata-editor",
        Name = "Metadata Editor",
        Category = "File & Data Tools",
        Description = "View and edit file metadata and EXIF data",
        Factory = () => null!
    }, new()
    {
        Key = "secure-file-shredder",
        Name = "Secure File Shredder",
        Category = "File & Data Tools",
        Description = "Securely delete files beyond recovery",
        Factory = () => null!
    }, new()
    {
        Key = "password-generator",
        Name = "Password Generator",
        Category = "Security & Privacy",
        Description = "Generate strong, customizable passwords",
        Factory = () => null!
    }, new()
    {
        Key = "hash-generator",
        Name = "Hash Generator",
        Category = "Security & Privacy",
        Description = "Compute cryptographic hashes from text or files",
        Factory = () => null!
    }, new()
    {
        Key = "certificate-inspector",
        Name = "Certificate Inspector",
        Category = "Security & Privacy",
        Description = "Inspect X.509 certificates and chains",
        Factory = () => null!
    }, new()
    {
        Key = "local-secret-vault",
        Name = "Local Secret Vault",
        Category = "Security & Privacy",
        Description = "Store and manage local encrypted secrets",
        Factory = () => null!
    }, new()
    {
        Key = "ping-tool",
        Name = "Ping Tool",
        Category = "Network & Internet",
        Description = "Ping hosts and measure round-trip times",
        Factory = () => null!
    }, new()
    {
        Key = "dns-lookup",
        Name = "DNS Lookup",
        Category = "Network & Internet",
        Description = "Query DNS records for any domain",
        Factory = () => null!
    }, new()
    {
        Key = "port-scanner",
        Name = "Port Scanner",
        Category = "Network & Internet",
        Description = "Scan open TCP ports on a host",
        Factory = () => null!
    }, new()
    {
        Key = "http-request-tester",
        Name = "HTTP Request Tester",
        Category = "Network & Internet",
        Description = "Send and inspect HTTP requests and responses",
        Factory = () => null!
    }, new()
    {
        Key = "network-speed-test",
        Name = "Network Speed Test",
        Category = "Network & Internet",
        Description = "Measure upload and download bandwidth",
        Factory = () => null!
    }, new()
    {
        Key = "downloader",
        Name = "Downloader",
        Category = "Network & Internet",
        Description = "Download files from URLs with queue support",
        Factory = () => null!
    }, new()
    {
        Key = "regex-tester",
        Name = "Regex Tester",
        Category = "Developer & Productivity",
        Description = "Test and debug regular expressions live",
        Factory = () => null!
    }, new()
    {
        Key = "base64-encoder",
        Name = "Base64 Encoder",
        Category = "Developer & Productivity",
        Description = "Encode and decode Base64 strings",
        Factory = () => null!
    }, new()
    {
        Key = "color-picker",
        Name = "Color Picker",
        Category = "Developer & Productivity",
        Description = "Pick and convert colours between formats",
        Factory = () => null!
    }, new()
    {
        Key = "diff-tool",
        Name = "Diff Tool",
        Category = "Developer & Productivity",
        Description = "Compare text blocks side by side",
        Factory = () => null!
    }, new()
    {
        Key = "json-yaml-validator",
        Name = "JSON/YAML Validator",
        Category = "Developer & Productivity",
        Description = "Validate and format JSON and YAML documents",
        Factory = () => null!
    }, new()
    {
        Key = "qr-code-generator",
        Name = "QR Code Generator",
        Category = "Developer & Productivity",
        Description = "Generate scannable QR codes from text or URLs",
        Factory = () => null!
    }, new()
    {
        Key = "text-format-converter",
        Name = "Text Format Converter",
        Category = "Developer & Productivity",
        Description = "Convert between Markdown, HTML, RTF, DOCX, and more",
        Factory = () => null!
    }, new()
    {
        Key = "timestamp-converter",
        Name = "Timestamp Converter",
        Category = "Developer & Productivity",
        Description = "Convert Unix timestamps to human-readable dates",
        Factory = () => null!
    }, new()
    {
        Key = "uuid-generator",
        Name = "UUID Generator",
        Category = "Developer & Productivity",
        Description = "Generate UUIDs, ULIDs, and random identifiers",
        Factory = () => null!
    }, new()
    {
        Key = "image-format-converter",
        Name = "Image Format Converter",
        Category = "Image Tools",
        Description = "Convert images between PNG, JPEG, WebP, and more",
        Factory = () => null!
    }, new()
    {
        Key = "image-resizer",
        Name = "Image Resizer",
        Category = "Image Tools",
        Description = "Resize images to specific dimensions or percentages",
        Factory = () => null!
    }, new()
    {
        Key = "screenshot-annotator",
        Name = "Screenshot Annotator",
        Category = "Image Tools",
        Description = "Annotate and mark up screenshots and images",
        Factory = () => null!
    }, ];
}