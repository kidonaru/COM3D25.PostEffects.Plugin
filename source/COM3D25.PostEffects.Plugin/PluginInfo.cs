using System;

namespace COM3D25.PostEffects.Plugin
{
    internal static class PluginInfo
    {
        public const string PluginName = "PostEffects";
#if COM3D25
        public const string PluginFullName = "COM3D25." + PluginName + ".Plugin";
#else
        // COM3D2 (2.0) 版は dll 名と揃える (UnityInjector のプラグイン登録名・ログ表記に使われる)
        public const string PluginFullName = "COM3D2." + PluginName + ".Plugin";
#endif
        public const string PluginVersion = "2.1.0.0";
        public const string WindowName = PluginName + " " + PluginVersion;

        public readonly static byte[] Icon = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAEvElEQVR42uWX30ubVxzG+2eMraZrpsa0XdLpWjVq1UV01bhosCqo" +
            "cf6223AuUt1qrFKHrplMkdKsI3NM2Exb2Ay56k2lQr0oSCmEXozmplAvBHshCOvVZ+cRXC8K/mjrfgYOvLzvSd7nnPP9Ps8nB157" +
            "3fr53zkO/JME8BePf6kAy2EHaUeKsDk9pB/3ke7wYTviIS21CIvFsX8CbHYXJ131FJb14vaOUuSbwlUdxuUJU1QyhTtvlMJ3ejlp" +
            "q8d2yPXqBKRY7OTm+vBW9dPUGqatJ0bbuSUa+hN4P3uIt/shDY0J2rxLtBXHaHo3jNfeT67VR8ob9pcTYLU6KH3PT3tziPMDMcZD" +
            "CSaurBOc3qB9ZIWqviRVZ5O0t6wQbNhgwrvO+KkE54/HaHeGKM3wY01xvJgAyyE7FW4/gY4ppr9cIDqzysz3awxdXKalJ47HP0u+" +
            "7yr5FVfxlM3SUhJnqHiZmYI1ooWrTJ9aIJAzRcUxP5aD9r0LcOf56GsNMTO+wO3oOnPfJhn+Ik5T8yTu8gCZeX4yMmvIcNSQ6fDj" +
            "dgZock4ynBVnriTJbd86M+UL9BWEcB/17U2A86iLrjP9RC7EuPvzKjcjScYHbtBYG8TlqsGamvX8cVmycFlraHQEGS+8wc0zSe5+" +
            "uEqkKkZXfj/OVNfuBVQW1nPp4zCLVxI8iK4RGY7TWR8k58TpHas7J+00ndlBIt44Dz5aY7EjwaUPwlRm1e9OQKopvC5vL3PBGI+v" +
            "rXNrapmRzklKC2qerfatY+TnlVD+fvXm0LXubT0vfbuGkbJJbnUu83jQHF+z2YXiXlLfdOwsINtRxGDDKAtfLfEkusH1oTjd1QEy" +
            "0rP+fLleerajl+DAxc2ha93bEpFxOIvuogDXW+M8Gdtg4dMlBitHyTYGtqMA9wkPoZYp7n+T4NF3K0R6Zqk1rbj1XKvVC6e/DvPL" +
            "T7HNoWvd07OtebUuP5HmWR6NrXD/QoJQvTGrTM/2Ag5abNS5W4iem+fpHDAPdybuMdZ9mYaqT2iv6+eHiV/5bXEVfufZx1zrnp5p" +
            "juaOdV7mzvg9uAZPf4RoYJ664pbNd2wvwEzSZH1JX9aP6Mf2LMCIlngtQovRorS4bQVsHoHZJm2Xtk3bp23Udu75CMyx6fh0jDpO" +
            "HauOd+ciNIWiglHhqIBUSCooFdaui9AUrApXBaxCVkGrsFXgO7ehaRW1jFpHLaRWUkuptXbdhqZl1bpqYbWyWlqtrRbfnREZ05B5" +
            "yERkJjIVmYtMZkcjMmYl05J5ycRkZjI1mdvurdjYpuxTNio7la3KXmWzslvZ7nNWbOxZNi27lm3LvmXjsnPZuux9b2FkAkRBokBR" +
            "sChgFDQKHAWPAkhBpEBSMCmgFFQKLAWXAkxBpkBTsO09jk2EKkoVqYpWRayiVpGr6FUEK4oVyYpmRbSiWpGt6FaEK8oV6Yr2FwMS" +
            "AxOCCsGFIEOwIegQfAhCBCOCEsGJIEWwImgRvAhiBDNWq+MlkcxglfBKmCXcEnYJv4RhwjFhmfBMmCZcE7YJ34RxwrlXB6UGNAWc" +
            "Ak8BqEBUQCowFaAKVAWsAlcB7P5huUFvIbhQXEguNBeiC9WF7P/9Pyb7KeD/+e/4D3JVgsFDNj5mAAAAAElFTkSuQmCC");
    }
}
