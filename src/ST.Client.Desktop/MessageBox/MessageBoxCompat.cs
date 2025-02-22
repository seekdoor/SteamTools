using System.Application;
using System.Application.Models.Settings;
using System.Application.Services;
using System.Application.UI.ViewModels;
using System.Properties;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace System.Windows
{
    /// <summary>
    /// 显示消息框。
    /// </summary>
    public static partial class MessageBoxCompat
    {
        const string default_caption = ThisAssembly.AssemblyTrademark;
        const MessageBoxButtonCompat default_button = MessageBoxButtonCompat.OK;
        static readonly Lazy<IMessageBoxCompatService?> mbcs =
            new(DI.Get_Nullable<IMessageBoxCompatService>);

        /// <inheritdoc cref="IMessageBoxCompatService.ShowAsync(string, string, MessageBoxButtonCompat, MessageBoxImageCompat?)"/>
        public static async Task<MessageBoxResultCompat> ShowAsync(
            string messageBoxText, string caption, MessageBoxButtonCompat button, MessageBoxImageCompat? icon = null, MessageBoxRememberChooseCompat rememberChooseKey = MessageBoxRememberChooseCompat.Undefined)
        {
            if (mbcs.Value != null)
            {
                return await mbcs.Value.ShowAsync(messageBoxText, caption, button, icon);
            }

            var isDoNotShow = rememberChooseKey != MessageBoxRememberChooseCompat.Undefined;

            if (isDoNotShow &&
                UISettings.DoNotShowMessageBoxs.Value?.Contains(rememberChooseKey) == true)
            {
                return MessageBoxResultCompat.OK;
            }

            var viewModel = new MessageBoxWindowViewModel
            {
                Content = messageBoxText,
                IsCancelcBtn = button == MessageBoxButtonCompat.OKCancel,
                IsShowRememberChoose = isDoNotShow,
            };

            var r = await IShowWindowService.Instance.ShowDialog(
                CustomWindow.MessageBox, viewModel, caption, ResizeModeCompat.NoResize);

            if (r && viewModel.RememberChoose && isDoNotShow)
            {
                if (UISettings.DoNotShowMessageBoxs.Value?.Contains(rememberChooseKey) == false)
                    UISettings.DoNotShowMessageBoxs.Value?.Add(rememberChooseKey);
                UISettings.DoNotShowMessageBoxs.RaiseValueChanged();
            }

            return r ? MessageBoxResultCompat.OK : MessageBoxResultCompat.Cancel;
        }

        /// <inheritdoc cref="IMessageBoxCompatService.ShowAsync(string, string, MessageBoxButtonCompat, MessageBoxImageCompat?)"/>
        public static async void Show(string messageBoxText, string caption = default_caption, MessageBoxButtonCompat button = default_button, MessageBoxImageCompat? icon = null)
        {
            await ShowAsync(messageBoxText, caption, button, icon);
        }

        /// <inheritdoc cref="IMessageBoxCompatService.ShowAsync(string, string, MessageBoxButtonCompat, MessageBoxImageCompat?)"/>
        public static Task<MessageBoxResultCompat> ShowAsync(Exception exception, string caption, MessageBoxButtonCompat button, MessageBoxImageCompat? icon = null)
        {
            var messageBoxText = exception.GetAllMessage();
            return ShowAsync(messageBoxText, caption, button, icon);
        }

        /// <inheritdoc cref="IMessageBoxCompatService.ShowAsync(string, string, MessageBoxButtonCompat, MessageBoxImageCompat?)"/>
        public static void Show(Exception exception, string caption = default_caption, MessageBoxButtonCompat button = default_button, MessageBoxImageCompat? icon = null)
        {
            var messageBoxText = exception.GetAllMessage();
            Show(messageBoxText, caption, button, icon);
        }
    }

}