﻿#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using AppleAuth.IOS.Interfaces;

namespace AppleAuth.IOS
{
    internal static class NativeMessageHandler
    {
        private delegate void NativeMessageHandlerDelegate(uint requestId, string messagePayload);

        private static readonly Dictionary<uint, MessageCallbackEntry> CallbackDictionary = new Dictionary<uint, MessageCallbackEntry>();
        private static uint _callbackId = 1;
        private static bool _initialized = false;

        internal static uint AddMessageCallback(IMessageHandlerScheduler scheduler, bool isSingleUse, Action<string> messageCallback)
        {
            if (!_initialized)
            {
                PInvoke.AppleAuth_IOS_SetupNativeMessageHandlerCallback(NativeMessageHandlerCallback);
                _initialized = true;
            }

            if (messageCallback == null)
                throw new Exception("Can't add a null Message Callback.");
            
            var usedCallbackId = _callbackId;
            _callbackId += 1;
            if (CallbackDictionary.ContainsKey(usedCallbackId))
                throw new Exception("A Message Callback with the same ID " + usedCallbackId + " already exists.");

            var callbackEntry = new MessageCallbackEntry(messageCallback, scheduler, isSingleUse);
            CallbackDictionary.Add(usedCallbackId, callbackEntry);
            return usedCallbackId;
        }
        
        internal static void ReplaceMessageCallback(uint requestId, IMessageHandlerScheduler scheduler, bool isSingleUse, Action<string> newMessageCallback)
        {
            if (CallbackDictionary.ContainsKey(requestId))
                throw new Exception($"Callback with id {requestId} does not exist and can't be replaced");

            CallbackDictionary.Remove(requestId);

            if (newMessageCallback == null)
                return;
            
            var callbackEntry = new MessageCallbackEntry(newMessageCallback, scheduler, isSingleUse);
            CallbackDictionary.Add(requestId, callbackEntry);
        }

        [MonoPInvokeCallback(typeof(NativeMessageHandlerDelegate))]
        private static void NativeMessageHandlerCallback(uint requestId, string messagePayload)
        {
            MessageCallbackEntry callbackEntry;
            if (!CallbackDictionary.TryGetValue(requestId, out callbackEntry))
                throw new Exception("A Message Callback with ID " + requestId + " couldn't be found");

            callbackEntry.Scheduler.Schedule(() => callbackEntry.MessageCallback.Invoke(messagePayload));
            if (callbackEntry.IsSingleUseCallback)
                CallbackDictionary.Remove(requestId);
        }
        
        private class MessageCallbackEntry
        {
            public readonly Action<string> MessageCallback;
            public readonly IMessageHandlerScheduler Scheduler;
            public readonly bool IsSingleUseCallback;

            public MessageCallbackEntry(Action<string> messageCallback, IMessageHandlerScheduler scheduler, bool isSingleUseCallback)
            {
                this.MessageCallback = messageCallback;
                this.Scheduler = scheduler;
                this.IsSingleUseCallback = isSingleUseCallback;
            }
        }
        
        private static class PInvoke
        {
            [DllImport("__Internal")]
            public static extern void AppleAuth_IOS_SetupNativeMessageHandlerCallback(NativeMessageHandlerDelegate callback);
        }
    }
}
#endif
