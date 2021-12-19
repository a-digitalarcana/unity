
mergeInto(LibraryManager.library, {
    GetHostAddress: function () { ReactUnityWebGL.GetHostAddress(); },
    GetWalletAddress: function () { ReactUnityWebGL.GetWalletAddress(); },
    BuyCardPack: function () { ReactUnityWebGL.BuyCardPack(); },
    RefundCardPack: function () { ReactUnityWebGL.RefundCardPack(); },
    OpenCardPack: function () { ReactUnityWebGL.OpenCardPack(); },
});
