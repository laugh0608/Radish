package com.example.radish_flutter

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class RadishNativeIntentPayloadsTest {
    @Test
    fun forumHandoffPayloadKeepsLargeIdsAsStrings() {
        val payload = RadishNativeIntentPayloads.forumHandoffPayload(
            postId = " 2042219067430928384 ",
            commentId = " 2042219067430928999 ",
            initialTitle = " Native discover wiring plan ",
            source = " notification ",
        )

        assertEquals(
            "{\"postId\":\"2042219067430928384\",\"source\":\"notification\",\"initialTitle\":\"Native discover wiring plan\",\"commentId\":\"2042219067430928999\"}",
            payload,
        )
    }

    @Test
    fun forumHandoffPayloadDefaultsToNotificationSource() {
        val payload = RadishNativeIntentPayloads.forumHandoffPayload(
            postId = "post-42",
            commentId = null,
            initialTitle = null,
            source = " ",
        )

        assertEquals(
            "{\"postId\":\"post-42\",\"source\":\"notification\"}",
            payload,
        )
    }

    @Test
    fun forumHandoffPayloadIgnoresBlankPostId() {
        val payload = RadishNativeIntentPayloads.forumHandoffPayload(
            postId = " ",
            commentId = "comment-1",
            initialTitle = "Ignored",
            source = "notification",
        )

        assertNull(payload)
    }

    @Test
    fun authCallbackPayloadParsesLoginCode() {
        val payload = RadishNativeIntentPayloads.authCallbackPayload(
            "radish://oidc/callback?code=native-code-1",
        )

        assertEquals(
            "{\"type\":\"login\",\"code\":\"native-code-1\"}",
            payload,
        )
    }

    @Test
    fun authCallbackPayloadParsesBrowserCancellation() {
        val payload = RadishNativeIntentPayloads.authCallbackPayload(
            "radish://oidc/callback?error=access_denied&error_description=User+canceled",
        )

        assertEquals(
            "{\"type\":\"login\",\"error\":\"access_denied\",\"errorDescription\":\"User canceled\"}",
            payload,
        )
    }

    @Test
    fun authCallbackPayloadParsesLogoutCompletion() {
        val payload = RadishNativeIntentPayloads.authCallbackPayload(
            "radish://oidc/logout-complete",
        )

        assertEquals("{\"type\":\"logout\"}", payload)
    }

    @Test
    fun authCallbackPayloadIgnoresOtherUris() {
        assertNull(RadishNativeIntentPayloads.authCallbackPayload(null))
        assertNull(RadishNativeIntentPayloads.authCallbackPayload("https://localhost/callback?code=1"))
        assertNull(RadishNativeIntentPayloads.authCallbackPayload("radish://other/callback?code=1"))
        assertNull(RadishNativeIntentPayloads.authCallbackPayload("radish://oidc/unknown?code=1"))
    }
}
