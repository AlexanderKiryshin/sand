using System.Runtime.InteropServices;

namespace YG
{
    public partial class PlatformYG2
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal", EntryPoint = "InitYandexMetrica")]
        private static extern void InitSandMetrica(string counterId);
#endif
        public void InitMetrica()
        {
            var settings = YG2.infoYG.Metrica;
            if (!settings.enable || !settings.useYandexMetrica || settings.metricaCounterID <= 0) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            InitSandMetrica(settings.metricaCounterID.ToString());
#endif
        }
    }
}
