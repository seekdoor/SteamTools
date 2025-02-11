//#if DEBUG
//using ReactiveUI;
//using System.Application.Services;
//using System.Application.UI.Resx;
//using System.Threading.Tasks;
//using System.Windows;
//#if __MOBILE__
//using WindowViewModel = System.Application.UI.ViewModels.PageViewModel;
//#endif

//namespace System.Application.UI.ViewModels
//{
//    [Obsolete("use TextBoxWindowViewModel")]
//    public class PasswordWindowViewModel : WindowViewModel, ITextBoxWindowViewModel
//    {
//        public PasswordWindowViewModel() : base()
//        {
//            Title = AppResources.LocalAuth_PasswordRequired;
//        }

//        private string? _Password;
//        public string? Password
//        {
//            get => _Password;
//            set => this.RaiseAndSetIfChanged(ref _Password, value);
//        }

//        string? ITextBoxWindowViewModel.Value { get => Password; set => Password = value; }

//        [Obsolete("use TextBoxWindowViewModel.ShowDialogAsync(TextBoxType.LocalAuth_PasswordRequired)")]
//        public static async Task<string?> ShowPasswordDialog()
//        {
//            return await TextBoxWindowViewModel.ShowDialogByPresetAsync(TextBoxWindowViewModel.PresetType.LocalAuth_PasswordRequired);
//        }

//        public bool InputValidator()
//        {
//            if (string.IsNullOrWhiteSpace(Password))
//            {
//                Toast.Show(AppResources.LocalAuth_ProtectionAuth_PasswordErrorTip);
//                return false;
//            }
//            return true;
//        }

//#if !__MOBILE__
//        public void Ok()
//        {
//            if (!InputValidator())
//            {
//                return;
//            }
//            this.Close();
//        }

//        public void Cancel()
//        {
//            Password = string.Empty;
//            this.Close();
//        }
//#endif
//    }
//}
//#endif