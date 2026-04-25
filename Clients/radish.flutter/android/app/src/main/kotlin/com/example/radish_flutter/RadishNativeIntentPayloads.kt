package com.example.radish_flutter

import java.net.URI
import java.net.URLDecoder

internal object RadishNativeIntentPayloads {
    fun forumHandoffPayload(
        postId: String?,
        commentId: String?,
        initialTitle: String?,
        source: String?,
    ): String? {
        val normalizedPostId = postId.normalizedOrNull() ?: return null

        return jsonObject(
            "postId" to normalizedPostId,
            "source" to (source.normalizedOrNull() ?: "notification"),
            "initialTitle" to initialTitle.normalizedOrNull(),
            "commentId" to commentId.normalizedOrNull(),
        )
    }

    fun authCallbackPayload(rawUri: String?): String? {
        val uri = rawUri.normalizedOrNull()?.let(::parseUriOrNull) ?: return null
        if (uri.scheme != "radish" || uri.host != "oidc") {
            return null
        }

        val query = parseQuery(uri.rawQuery)
        return when (uri.path?.trim()) {
            "/callback" -> jsonObject(
                "type" to "login",
                "code" to query["code"].normalizedOrNull(),
                "error" to query["error"].normalizedOrNull(),
                "errorDescription" to query["error_description"].normalizedOrNull(),
            )
            "/logout-complete" -> jsonObject("type" to "logout")
            else -> null
        }
    }

    private fun parseUriOrNull(rawUri: String): URI? {
        return try {
            URI(rawUri)
        } catch (_: IllegalArgumentException) {
            null
        }
    }

    private fun parseQuery(rawQuery: String?): Map<String, String> {
        if (rawQuery.isNullOrBlank()) {
            return emptyMap()
        }

        return rawQuery
            .split("&")
            .mapNotNull { pair ->
                if (pair.isBlank()) {
                    return@mapNotNull null
                }

                val separatorIndex = pair.indexOf("=")
                val rawKey = if (separatorIndex >= 0) {
                    pair.substring(0, separatorIndex)
                } else {
                    pair
                }
                val rawValue = if (separatorIndex >= 0) {
                    pair.substring(separatorIndex + 1)
                } else {
                    ""
                }
                val key = decodeQueryPart(rawKey).normalizedOrNull()
                if (key == null) {
                    null
                } else {
                    key to decodeQueryPart(rawValue)
                }
            }
            .toMap()
    }

    private fun decodeQueryPart(value: String): String {
        return URLDecoder.decode(value, Charsets.UTF_8.name())
    }

    private fun jsonObject(vararg fields: Pair<String, String?>): String {
        return fields
            .mapNotNull { (key, value) ->
                value?.let { "\"${escapeJson(key)}\":\"${escapeJson(it)}\"" }
            }
            .joinToString(prefix = "{", postfix = "}", separator = ",")
    }

    private fun escapeJson(value: String): String {
        return buildString {
            for (char in value) {
                when (char) {
                    '\\' -> append("\\\\")
                    '"' -> append("\\\"")
                    '\b' -> append("\\b")
                    '\u000C' -> append("\\f")
                    '\n' -> append("\\n")
                    '\r' -> append("\\r")
                    '\t' -> append("\\t")
                    else -> {
                        if (char.code < 0x20) {
                            append("\\u")
                            append(char.code.toString(16).padStart(4, '0'))
                        } else {
                            append(char)
                        }
                    }
                }
            }
        }
    }

    private fun String?.normalizedOrNull(): String? {
        val normalized = this?.trim()
        return if (normalized.isNullOrEmpty()) null else normalized
    }
}
