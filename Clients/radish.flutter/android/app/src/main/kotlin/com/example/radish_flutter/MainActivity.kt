package com.example.radish_flutter

import android.content.Context
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            SESSION_STORE_CHANNEL,
        ).setMethodCallHandler { call, result ->
            val preferences = getSharedPreferences(SESSION_STORE_PREFERENCES, Context.MODE_PRIVATE)

            when (call.method) {
                "read" -> result.success(preferences.getString(SESSION_STORE_KEY, null))
                "write" -> {
                    val payload = call.arguments as? String
                    if (payload.isNullOrBlank()) {
                        result.error("invalid_payload", "Session payload must be a non-empty string.", null)
                        return@setMethodCallHandler
                    }

                    preferences.edit().putString(SESSION_STORE_KEY, payload).apply()
                    result.success(null)
                }
                "clear" -> {
                    preferences.edit().remove(SESSION_STORE_KEY).apply()
                    result.success(null)
                }
                else -> result.notImplemented()
            }
        }
    }

    companion object {
        private const val SESSION_STORE_CHANNEL = "radish.flutter/session_store"
        private const val SESSION_STORE_PREFERENCES = "radish_flutter_session_store"
        private const val SESSION_STORE_KEY = "auth_session"
    }
}
