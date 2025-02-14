#if __ANDROID__
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Util;
using Android.Opengl;
using XEPlatform = Xamarin.Essentials.Platform;
using Process = System.Diagnostics.Process;
#endif
using System.Properties;
using System.Text;
using System.Windows;
using System.Linq;
using System.Diagnostics;
using System.Application.Services;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace System.Application.UI
{
    static class AboutAppInfoPopup
    {
        static int show_runtime_info_counter;
        static DateTime show_runtime_info_last_click_time;
        const int show_runtime_info_counter_max = 5;
        const double show_runtime_info_click_effective_interval = 1.5;
        const string os_ver =
#if __ANDROID__
            "[os.ver] Android ";
#else
            "[os.ver] ";
#endif

        public static void OnClick()
        {
            var now = DateTime.Now;
            if (show_runtime_info_last_click_time == default || (now - show_runtime_info_last_click_time).TotalSeconds <= show_runtime_info_click_effective_interval)
            {
                show_runtime_info_counter++;
            }
            else
            {
                show_runtime_info_counter = 1;
            }
            show_runtime_info_last_click_time = now;
            if (show_runtime_info_counter >= show_runtime_info_counter_max)
            {
                show_runtime_info_counter = 0;
                show_runtime_info_last_click_time = default;

                StringBuilder b = new(os_ver);
#if __ANDROID__
                var activity = XEPlatform.CurrentActivity;
                var sdkInt = Build.VERSION.SdkInt;
                b.AppendFormat("{0}(API {1})", sdkInt, (int)sdkInt);
#else
                if (OperatingSystem2.IsWindows)
                {
                    var dps = IDesktopPlatformService.Instance;
#pragma warning disable CA1416 // 验证平台兼容性
                    var productName = dps.WindowsProductName;
                    var major = Environment.OSVersion.Version.Major;
                    var minor = Environment.OSVersion.Version.Minor;
                    var build = Environment.OSVersion.Version.Build;
                    var revision = dps.WindowsVersionRevision;
                    b.AppendFormat("{0} {1}.{2}.{3}.{4}", productName, major, minor, build, revision);
                    var servicePack = Environment.OSVersion.ServicePack;
                    if (!string.IsNullOrEmpty(servicePack))
                    {
                        b.Append(' ');
                        b.Append(servicePack);
                    }
                    var releaseId = dps.WindowsReleaseIdOrDisplayVersion;
#pragma warning restore CA1416 // 验证平台兼容性
                    if (!string.IsNullOrWhiteSpace(releaseId))
                    {
                        b.Append(" (");
                        b.Append(releaseId);
                        b.Append(')');
                    }
                }
                else
                {
                    b.AppendFormat("{0} {1}", DeviceInfo2.OSName, Environment.OSVersion.Version);
                }
#endif
                b.AppendLine();

#if __ANDROID__
                GetAppDisplayVersion(activity, b);
                static void GetAppDisplayVersion(Context context, StringBuilder b)
                {
                    var info = context.PackageManager!.GetPackageInfo(context.PackageName!, default);
                    if (info == default) return;
#pragma warning disable CS0618 // 类型或成员已过时
                    b.AppendFormat("{0}({1})", info.VersionName, Build.VERSION.SdkInt >= BuildVersionCodes.P ? info.LongVersionCode : info.VersionCode);
#pragma warning restore CS0618 // 类型或成员已过时
                }
#else
                b.AppendFormat("[app.ver] {0}", ThisAssembly.DynamicVersion);
#endif
                b.AppendLine();

                b.Append("[app.flavor] ");
#if __ANDROID__
                b.AppendLine(
#if IS_STORE_PACKAGE
                        "store"
#else
#endif
                        );
#else
                if (DesktopBridge.IsRunningAsUwp)
                {
                    b.Append("ms-store");
                }
                b.AppendLine();
#endif

                b.Append("[clr.ver] ");
                string? clrVersion;
                try
                {
                    clrVersion = typeof(object).Assembly.GetRequiredCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                }
                catch
                {
                    clrVersion = null;
                }
                if (string.IsNullOrEmpty(clrVersion))
                    b.Append(Environment.Version);
                else
                    b.Append(clrVersion);
                b.AppendLine();

#if __ANDROID__
                b.Append("[app.starttime] ");
                b.AppendLine(MainApplication.ElapsedMilliseconds + "ms");

                //if (_ThisAssembly.Debuggable)
                //{
                //    b.Append("[app.multi] ");
                //    VirtualApkCheckUtil.GetCheckResult(AndroidApplication.Context, b);
                //    b.AppendLine();
                //}

                //b.Append("[rom.ver] ");
                //AndroidROM.Current.ToString(b);
                //b.AppendLine();

                b.Append("[app.center] ");
                b.AppendLine(VisualStudioAppCenterSDK.TryGetAppSecret(out var appSecret) ? appSecret.Split('-').FirstOrDefault() : string.Empty);

                b.Append("[webview.ver] ");
                GetWebViewImplementationVersionDisplayString(b);
                b.AppendLine();
                static void GetWebViewImplementationVersionDisplayString(StringBuilder b)
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        var webViewPackage = WebView.CurrentWebViewPackage;
                        if (webViewPackage != default)
                        {
                            var packageName = webViewPackage.PackageName;
                            var packageVersion = webViewPackage.VersionName;
                            if (string.Equals(packageName, "com.android.webview", StringComparison.OrdinalIgnoreCase) || string.Equals(packageName, "com.google.android.webview", StringComparison.OrdinalIgnoreCase))
                            {
                                packageName = "asw"; // Android System Webview
                            }
                            else if (string.Equals(packageName, "com.android.chrome", StringComparison.OrdinalIgnoreCase))
                            {
                                packageName = "chrome"; // Chrome
                            }
                            b.AppendFormat("{0}({1})", packageVersion, packageName);
                            return;
                        }
                    }
                }
#endif

                b.Append("[time] ");
                GetTime(b);
                static void GetTime(StringBuilder b)
                {
                    string timeString;
                    const string f = "yy-MM-dd HH:mm:ss";
                    const string f2 = "HH:mm:ss";
                    const string f3 = "dd HH:mm:ss";
                    var time = Process.GetCurrentProcess().StartTime;
                    time = time.ToLocalTime();
                    var utc_time = time.ToUniversalTime();
                    var local = TimeZoneInfo.Local;
                    timeString = utc_time.Hour == time.Hour
                        ? time.ToString(time.Year >= 2100 ? DateTimeFormat.Standard : f)
                        : utc_time.Day == time.Day
                        ? $"{utc_time.ToString(f)}({time.ToString(f2)} {local.StandardName})"
                        : $"{utc_time.ToString(f)}({time.ToString(f3)} {local.StandardName})";
                    b.Append(timeString);
                }
                b.AppendLine();

#if __ANDROID__
                b.Append("[screen] ");
                var metrics = new DisplayMetrics();
                activity.WindowManager?.DefaultDisplay?.GetRealMetrics(metrics);
                GetScreen(activity, metrics, b);
                static void GetScreen(Context context, DisplayMetrics metrics, StringBuilder b)
                {
                    var screen_w = metrics.WidthPixels;
                    var screen_h = metrics.HeightPixels;
                    var screen_max = Math.Max(screen_w, screen_h);
                    var screen_min = screen_max == screen_w ? screen_h : screen_w;
                    var configuration = context.Resources?.Configuration;
                    var screen_dp_w = configuration?.ScreenWidthDp ?? 0;
                    var screen_dp_h = configuration?.ScreenHeightDp ?? 0;
                    var screen_dp_max = Math.Max(screen_dp_w, screen_dp_h);
                    var screen_dp_min = screen_max == screen_dp_w ? screen_dp_h : screen_dp_w;
                    b.AppendFormat("{0}x{1}({2}x{3})", screen_max, screen_min, screen_dp_max, screen_dp_min);
                    var dpi = (int)metrics.DensityDpi;
                    b.AppendFormat(" {0}dpi", dpi);
                    if (dpi < (int)DisplayMetricsDensity.Low)
                    {
                        b.Append("(<ldpi)");
                    }
                    else if (dpi == (int)DisplayMetricsDensity.Low)
                    {
                        b.Append("(ldpi)");
                    }
                    else if (dpi < (int)DisplayMetricsDensity.Medium)
                    {
                        b.Append("(ldpi~mdpi)");
                    }
                    else if (dpi == (int)DisplayMetricsDensity.Medium)
                    {
                        b.Append("(mdpi)");
                    }
                    else if (dpi == (int)DisplayMetricsDensity.Tv)
                    {
                        b.Append("(tv)");
                    }
                    else if (dpi < (int)DisplayMetricsDensity.High)
                    {
                        b.Append("(mdpi~hdpi)");
                    }
                    else if (dpi == (int)DisplayMetricsDensity.High)
                    {
                        b.Append("(hdpi)");
                    }
                    else if (dpi < (int)DisplayMetricsDensity.Xhigh)
                    {
                        b.Append("(hdpi~xhdpi)");
                    }
                    else if (dpi == (int)DisplayMetricsDensity.Xhigh)
                    {
                        b.Append("(xhdpi)");
                    }
                    else if (dpi < (int)DisplayMetricsDensity.Xxhigh)
                    {
                        b.Append("(xhdpi~xxhdpi)");
                    }
                    else if (dpi == (int)DisplayMetricsDensity.Xxhigh)
                    {
                        b.Append("(xxhdpi)");
                    }
                    else if (dpi < (int)DisplayMetricsDensity.Xxxhigh)
                    {
                        b.Append("(xxhdpi~xxxhdpi)");
                    }
                    else if (dpi == (int)DisplayMetricsDensity.Xxxhigh)
                    {
                        b.Append("(xxxhdpi)");
                    }
                }
                b.AppendLine();

                //b.Append("[screen.notch] ");
                //b.Append(ScreenCompatUtil.IsNotch(this).ToLowerString());
                //b.AppendLine();

                //b.Append("[screen.notch.hide] ");
                //b.Append(ScreenCompatUtil.IsHideNotch(this).ToLowerString());
                //b.AppendLine();

                //b.Append("[screen.full.gestures] ");
                //b.Append(ScreenCompatUtil.IsFullScreenGesture(this).ToLowerString());
                //b.AppendLine();

                static string GetJavaSystemGetProperty(string propertyKey)
                {
                    try
                    {
                        return Java.Lang.JavaSystem.GetProperty(propertyKey) ?? "";
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
                b.Append("[jvm.ver] ");
                b.Append(GetJavaSystemGetProperty("java.vm.version"));
                b.AppendLine();

                b.Append("[mono.ver] ");
                b.Append(Mono.Runtime.GetDisplayName());
                b.AppendLine();

                b.Append("[kernel.ver] ");
                b.Append(GetJavaSystemGetProperty("os.version"));
                b.AppendLine();

                b.Append("[device] ");
                b.Append(Build.Device ?? "");
                b.AppendLine();
                b.Append("[device.model] ");
                b.Append(Build.Model ?? "");
                b.AppendLine();
                b.Append("[device.product] ");
                b.Append(Build.Product ?? "");
                b.AppendLine();
                b.Append("[device.brand] ");
                b.Append(Build.Brand ?? "");
                b.AppendLine();
                b.Append("[device.manufacturer] ");
                b.Append(Build.Manufacturer ?? "");
                b.AppendLine();
                b.Append("[device.fingerprint] ");
                b.Append(Build.Fingerprint ?? "");
                b.AppendLine();
                b.Append("[device.hardware] ");
                b.Append(Build.Hardware ?? "");
                b.AppendLine();
                b.Append("[device.tags] ");
                b.Append(Build.Tags ?? "");
                b.AppendLine();
                //if (ThisAssembly.Debuggable)
                //{
                //    b.Append("[device.arc] ");
                //    b.Append(DeviceSecurityCheckUtil.IsCompatiblePC(this).ToLowerString());
                //    b.AppendLine();
                //    b.Append("[device.emulator] ");
                //    b.Append(DeviceSecurityCheckUtil.IsEmulator.ToLowerString());
                //    b.AppendLine();
                //}
                b.Append("[device.gl.renderer] ");
                b.Append(GLES20.GlGetString(GLES20.GlRenderer) ?? "");
                b.AppendLine();
                b.Append("[device.gl.vendor] ");
                b.Append(GLES20.GlGetString(GLES20.GlVendor) ?? "");
                b.AppendLine();
                b.Append("[device.gl.version] ");
                b.Append(GLES20.GlGetString(GLES20.GlVersion) ?? "");
                b.AppendLine();
                b.Append("[device.gl.extensions] ");
                b.Append(GLES20.GlGetString(GLES20.GlExtensions) ?? "");
                b.AppendLine();
                b.Append("[device.biometric] ");
                b.Append(IBiometricService.Instance.IsSupportedAsync().Result.ToLowerString());
                b.AppendLine();
#endif

                b.Append("[xamarin.essentials.supported] ");
                static bool? GetXamarinEssentialsIsSupported()
                {
                    try
                    {
                        if (typeof(MainThread2).Assembly.GetType("System.Application.XamarinEssentials").GetProperty("IsSupported", BindingFlags.Public | BindingFlags.Static).GetValue(null) is bool b)
                            return b;
                    }
                    catch
                    {
                    }
                    return null;
                }
                b.Append(GetXamarinEssentialsIsSupported().ToLowerString());
                b.AppendLine();

                var b_str = b.ToString();
                MessageBoxCompat.Show(b_str, "");
            }
        }
    }
}