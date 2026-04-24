package com.example.radish_flutter

import android.content.Context
import android.content.Intent
import android.net.Uri
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import org.json.JSONObject

class MainActivity : FlutterActivity() {
    private var pendingForumHandoffPayload: String? = null
    private var pendingAuthCallbackPayload: String? = null

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        pendingForumHandoffPayload = resolvePendingHandoffPayload(intent)
        pendingAuthCallbackPayload = resolvePendingAuthCallbackPayload(intent)

        val preferences = getSharedPreferences(SESSION_STORE_PREFERENCES, Context.MODE_PRIVATE)

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            SESSION_STORE_CHANNEL,
        ).setMethodCallHandler { call, result ->
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

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            FORUM_FOLLOW_UP_CHANNEL,
        ).setMethodCallHandler { call, result ->
            when (call.method) {
                "takePendingHandoff" -> {
                    val payload = pendingForumHandoffPayload
                    pendingForumHandoffPayload = null
                    result.success(payload)
                }
                "readRecentBrowseHandoff" -> {
                    result.success(preferences.getString(FORUM_RECENT_BROWSE_KEY, null))
                }
                "readPendingPostLoginTarget" -> {
                    result.success(preferences.getString(SHELL_PENDING_POST_LOGIN_TARGET_KEY, null))
                }
                "writeRecentBrowseHandoff" -> {
                    val payload = call.arguments as? String
                    if (payload.isNullOrBlank()) {
                        result.error("invalid_payload", "Forum follow-up payload must be a non-empty string.", null)
                        return@setMethodCallHandler
                    }

                    preferences.edit().putString(FORUM_RECENT_BROWSE_KEY, payload).apply()
                    result.success(null)
                }
                "writePendingPostLoginTarget" -> {
                    val payload = call.arguments as? String
                    if (payload.isNullOrBlank()) {
                        result.error("invalid_payload", "Shell post-login payload must be a non-empty string.", null)
                        return@setMethodCallHandler
                    }

                    preferences.edit().putString(SHELL_PENDING_POST_LOGIN_TARGET_KEY, payload).apply()
                    result.success(null)
                }
                "clearRecentBrowseHandoff" -> {
                    preferences.edit().remove(FORUM_RECENT_BROWSE_KEY).apply()
                    result.success(null)
                }
                "clearPendingPostLoginTarget" -> {
                    preferences.edit().remove(SHELL_PENDING_POST_LOGIN_TARGET_KEY).apply()
                    result.success(null)
                }
                else -> result.notImplemented()
            }
        }

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            AUTH_FLOW_CHANNEL,
        ).setMethodCallHandler { call, result ->
            when (call.method) {
                "openAuthorizeUrl" -> {
                    val rawUrl = call.arguments as? String
                    if (rawUrl.isNullOrBlank()) {
                        result.error("invalid_url", "Authorize URL must be a non-empty string.", null)
                        return@setMethodCallHandler
                    }

                    openExternalUrl(rawUrl)
                    result.success(null)
                }
                "openLogoutUrl" -> {
                    val rawUrl = call.arguments as? String
                    if (rawUrl.isNullOrBlank()) {
                        result.error("invalid_url", "Logout URL must be a non-empty string.", null)
                        return@setMethodCallHandler
                    }

                    openExternalUrl(rawUrl)
                    result.success(null)
                }
                "takePendingCallback" -> {
                    val payload = pendingAuthCallbackPayload
                    pendingAuthCallbackPayload = null
                    result.success(payload)
                }
                else -> result.notImplemented()
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        pendingForumHandoffPayload = resolvePendingHandoffPayload(intent)
        pendingAuthCallbackPayload = resolvePendingAuthCallbackPayload(intent)
    }

    private fun resolvePendingHandoffPayload(intent: Intent?): String? {
        val postId = intent?.getStringExtra(EXTRA_FORUM_POST_ID)?.trim().orEmpty()
        if (postId.isBlank()) {
            return null
        }

        return JSONObject().apply {
            put("postId", postId)
            put(
                "source",
                intent?.getStringExtra(EXTRA_FORUM_SOURCE)?.trim().takeUnless { it.isNullOrBlank() }
                    ?: "notification",
            )

            intent?.getStringExtra(EXTRA_FORUM_INITIAL_TITLE)?.trim()?.takeUnless { it.isBlank() }?.let {
                put("initialTitle", it)
            }

            intent?.getStringExtra(EXTRA_FORUM_COMMENT_ID)?.trim()?.takeUnless { it.isBlank() }?.let {
                put("commentId", it)
            }
        }.toString()
    }

    private fun resolvePendingAuthCallbackPayload(intent: Intent?): String? {
        val data = intent?.data ?: return null
        if (!data.isRadishOidcCallback()) {
            return null
        }

        return JSONObject().apply {
            when (data.path?.trim()) {
                "/callback" -> {
                    put("type", "login")
                    data.getQueryParameter("code")?.trim()?.takeUnless { it.isBlank() }?.let {
                        put("code", it)
                    }
                    data.getQueryParameter("error")?.trim()?.takeUnless { it.isBlank() }?.let {
                        put("error", it)
                    }
                    data.getQueryParameter("error_description")?.trim()?.takeUnless { it.isBlank() }?.let {
                        put("errorDescription", it)
                    }
                }
                "/logout-complete" -> {
                    put("type", "logout")
                }
                else -> return null
            }
        }.toString()
    }

    private fun openExternalUrl(rawUrl: String) {
        val uri = Uri.parse(rawUrl)
        val intent = Intent(Intent.ACTION_VIEW, uri).apply {
            addCategory(Intent.CATEGORY_BROWSABLE)
        }
        startActivity(intent)
    }

    private fun Uri.isRadishOidcCallback(): Boolean {
        return scheme == "radish" && host == "oidc"
    }

    companion object {
        private const val SESSION_STORE_CHANNEL = "radish.flutter/session_store"
        private const val SESSION_STORE_PREFERENCES = "radish_flutter_session_store"
        private const val SESSION_STORE_KEY = "auth_session"
        private const val FORUM_FOLLOW_UP_CHANNEL = "radish.flutter/forum_follow_up"
        private const val AUTH_FLOW_CHANNEL = "radish.flutter/native_auth"
        private const val FORUM_RECENT_BROWSE_KEY = "forum_recent_browse_handoff"
        private const val SHELL_PENDING_POST_LOGIN_TARGET_KEY = "shell_pending_post_login_target"
        private const val EXTRA_FORUM_POST_ID = "forum_post_id"
        private const val EXTRA_FORUM_COMMENT_ID = "forum_comment_id"
        private const val EXTRA_FORUM_INITIAL_TITLE = "forum_initial_title"
        private const val EXTRA_FORUM_SOURCE = "forum_source"
    }
}
