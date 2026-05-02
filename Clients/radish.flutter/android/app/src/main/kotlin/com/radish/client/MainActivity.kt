package com.radish.client

import android.content.Context
import android.content.Intent
import android.net.Uri
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import org.json.JSONArray
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
                "readRecentBrowseHandoffs" -> {
                    result.success(readRecentBrowseHandoffs(preferences))
                }
                "readRecentProfileUserId" -> {
                    result.success(preferences.getString(PROFILE_RECENT_USER_ID_KEY, null))
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
                    writeRecentBrowseHandoff(preferences, payload)
                    result.success(null)
                }
                "writeRecentProfileUserId" -> {
                    val userId = call.arguments as? String
                    if (userId.isNullOrBlank()) {
                        result.error("invalid_user_id", "Profile user id must be a non-empty string.", null)
                        return@setMethodCallHandler
                    }

                    preferences.edit().putString(PROFILE_RECENT_USER_ID_KEY, userId.trim()).apply()
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
                    preferences.edit()
                        .remove(FORUM_RECENT_BROWSE_KEY)
                        .remove(FORUM_RECENT_BROWSE_LIST_KEY)
                        .apply()
                    result.success(null)
                }
                "clearRecentProfileUserId" -> {
                    preferences.edit().remove(PROFILE_RECENT_USER_ID_KEY).apply()
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
            DOCS_FOLLOW_UP_CHANNEL,
        ).setMethodCallHandler { call, result ->
            when (call.method) {
                "readRecentDocumentTarget" -> {
                    result.success(preferences.getString(DOCS_RECENT_DOCUMENT_KEY, null))
                }
                "readRecentDocumentTargets" -> {
                    result.success(readRecentDocumentTargets(preferences))
                }
                "writeRecentDocumentTarget" -> {
                    val payload = call.arguments as? String
                    if (payload.isNullOrBlank()) {
                        result.error("invalid_payload", "Docs follow-up payload must be a non-empty string.", null)
                        return@setMethodCallHandler
                    }

                    preferences.edit().putString(DOCS_RECENT_DOCUMENT_KEY, payload).apply()
                    writeRecentDocumentTarget(preferences, payload)
                    result.success(null)
                }
                "clearRecentDocumentTarget" -> {
                    preferences.edit()
                        .remove(DOCS_RECENT_DOCUMENT_KEY)
                        .remove(DOCS_RECENT_DOCUMENT_LIST_KEY)
                        .apply()
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

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            APP_LIFECYCLE_CHANNEL,
        ).setMethodCallHandler { call, result ->
            when (call.method) {
                "moveTaskToBack" -> {
                    moveTaskToBack(true)
                    result.success(null)
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
        return RadishNativeIntentPayloads.forumHandoffPayload(
            postId = intent?.getStringExtra(EXTRA_FORUM_POST_ID),
            commentId = intent?.getStringExtra(EXTRA_FORUM_COMMENT_ID),
            initialTitle = intent?.getStringExtra(EXTRA_FORUM_INITIAL_TITLE),
            source = intent?.getStringExtra(EXTRA_FORUM_SOURCE),
        )
    }

    private fun resolvePendingAuthCallbackPayload(intent: Intent?): String? {
        return RadishNativeIntentPayloads.authCallbackPayload(intent?.data?.toString())
    }

    private fun openExternalUrl(rawUrl: String) {
        val uri = Uri.parse(rawUrl)
        val intent = Intent(Intent.ACTION_VIEW, uri).apply {
            addCategory(Intent.CATEGORY_BROWSABLE)
        }
        startActivity(intent)
    }

    private fun readRecentBrowseHandoffs(preferences: android.content.SharedPreferences): String {
        val listPayload = preferences.getString(FORUM_RECENT_BROWSE_LIST_KEY, null)
        if (!listPayload.isNullOrBlank()) {
            return listPayload
        }

        val singlePayload = preferences.getString(FORUM_RECENT_BROWSE_KEY, null)
        if (singlePayload.isNullOrBlank()) {
            return JSONArray().toString()
        }

        return try {
            JSONArray().put(JSONObject(singlePayload)).toString()
        } catch (_: Exception) {
            JSONArray().toString()
        }
    }

    private fun writeRecentBrowseHandoff(
        preferences: android.content.SharedPreferences,
        payload: String,
    ) {
        val target = JSONObject(payload)
        val current = try {
            JSONArray(readRecentBrowseHandoffs(preferences))
        } catch (_: Exception) {
            JSONArray()
        }
        val next = JSONArray().put(target)
        var index = 0

        while (index < current.length() && next.length() < MAX_RECENT_BROWSE_TARGETS) {
            val item = current.optJSONObject(index)
            if (item != null && !isSameRecentBrowseTarget(item, target)) {
                next.put(item)
            }
            index += 1
        }

        preferences.edit().putString(FORUM_RECENT_BROWSE_LIST_KEY, next.toString()).apply()
    }

    private fun isSameRecentBrowseTarget(left: JSONObject, right: JSONObject): Boolean {
        return left.optString("postId").trim() == right.optString("postId").trim() &&
            left.optString("commentId").trim() == right.optString("commentId").trim()
    }

    private fun readRecentDocumentTargets(preferences: android.content.SharedPreferences): String {
        val listPayload = preferences.getString(DOCS_RECENT_DOCUMENT_LIST_KEY, null)
        if (!listPayload.isNullOrBlank()) {
            return listPayload
        }

        val singlePayload = preferences.getString(DOCS_RECENT_DOCUMENT_KEY, null)
        if (singlePayload.isNullOrBlank()) {
            return JSONArray().toString()
        }

        return try {
            JSONArray().put(JSONObject(singlePayload)).toString()
        } catch (_: Exception) {
            JSONArray().toString()
        }
    }

    private fun writeRecentDocumentTarget(
        preferences: android.content.SharedPreferences,
        payload: String,
    ) {
        val target = JSONObject(payload)
        val current = try {
            JSONArray(readRecentDocumentTargets(preferences))
        } catch (_: Exception) {
            JSONArray()
        }
        val next = JSONArray().put(target)
        var index = 0

        while (index < current.length() && next.length() < MAX_RECENT_DOCUMENT_TARGETS) {
            val item = current.optJSONObject(index)
            if (item != null && !isSameRecentDocumentTarget(item, target)) {
                next.put(item)
            }
            index += 1
        }

        preferences.edit().putString(DOCS_RECENT_DOCUMENT_LIST_KEY, next.toString()).apply()
    }

    private fun isSameRecentDocumentTarget(left: JSONObject, right: JSONObject): Boolean {
        return left.optString("slug").trim() == right.optString("slug").trim()
    }

    companion object {
        private const val SESSION_STORE_CHANNEL = "radish.flutter/session_store"
        private const val SESSION_STORE_PREFERENCES = "radish_flutter_session_store"
        private const val SESSION_STORE_KEY = "auth_session"
        private const val FORUM_FOLLOW_UP_CHANNEL = "radish.flutter/forum_follow_up"
        private const val DOCS_FOLLOW_UP_CHANNEL = "radish.flutter/docs_follow_up"
        private const val AUTH_FLOW_CHANNEL = "radish.flutter/native_auth"
        private const val APP_LIFECYCLE_CHANNEL = "radish.flutter/app_lifecycle"
        private const val FORUM_RECENT_BROWSE_KEY = "forum_recent_browse_handoff"
        private const val FORUM_RECENT_BROWSE_LIST_KEY = "forum_recent_browse_handoffs"
        private const val DOCS_RECENT_DOCUMENT_KEY = "docs_recent_document_target"
        private const val DOCS_RECENT_DOCUMENT_LIST_KEY = "docs_recent_document_targets"
        private const val PROFILE_RECENT_USER_ID_KEY = "profile_recent_user_id"
        private const val SHELL_PENDING_POST_LOGIN_TARGET_KEY = "shell_pending_post_login_target"
        private const val MAX_RECENT_BROWSE_TARGETS = 5
        private const val MAX_RECENT_DOCUMENT_TARGETS = 5
        private const val EXTRA_FORUM_POST_ID = "forum_post_id"
        private const val EXTRA_FORUM_COMMENT_ID = "forum_comment_id"
        private const val EXTRA_FORUM_INITIAL_TITLE = "forum_initial_title"
        private const val EXTRA_FORUM_SOURCE = "forum_source"
    }
}
