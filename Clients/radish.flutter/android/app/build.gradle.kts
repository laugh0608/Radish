import java.util.Properties
import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

val releaseKeystorePropertiesFile = rootProject.file("key.properties")
val releaseKeystoreProperties = Properties().apply {
    if (releaseKeystorePropertiesFile.exists()) {
        releaseKeystorePropertiesFile.inputStream().use { load(it) }
    }
}

fun releaseSigningProperty(name: String): String? =
    releaseKeystoreProperties.getProperty(name)?.trim()?.takeIf { it.isNotEmpty() }

fun isPlaceholderReleaseSigningProperty(value: String): Boolean =
    value.startsWith("replace-with-", ignoreCase = true) ||
        (value.startsWith("<") && value.endsWith(">"))

val releaseSigningRequiredProperties =
    listOf("storeFile", "storePassword", "keyAlias", "keyPassword")

val releaseSigningIssues = mutableListOf<String>().apply {
    if (!releaseKeystorePropertiesFile.exists()) {
        add("android/key.properties was not found; release builds will use debug signing.")
    } else {
        releaseSigningRequiredProperties.forEach { name ->
            val value = releaseSigningProperty(name)
            if (value == null) {
                add("android/key.properties is missing '$name'.")
            } else if (isPlaceholderReleaseSigningProperty(value)) {
                add("android/key.properties still contains a placeholder for '$name'.")
            }
        }

        releaseSigningProperty("storeFile")?.let { storeFile ->
            if (!isPlaceholderReleaseSigningProperty(storeFile) && !rootProject.file(storeFile).exists()) {
                add("release keystore file '$storeFile' was not found under android/.")
            }
        }
    }
}

val hasReleaseSigningConfig =
    releaseKeystorePropertiesFile.exists() && releaseSigningIssues.isEmpty()

if (releaseKeystorePropertiesFile.exists() && !hasReleaseSigningConfig) {
    throw GradleException(
        "Invalid Android release signing configuration:\n" +
            releaseSigningIssues.joinToString(separator = "\n") { "- $it" } +
            "\nRemove android/key.properties to use debug signing locally, or fix the file for RC distribution."
    )
}

android {
    namespace = "com.radish.client"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        applicationId = "com.radish.client"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        if (hasReleaseSigningConfig) {
            create("release") {
                storeFile = rootProject.file(releaseSigningProperty("storeFile")!!)
                storePassword = releaseSigningProperty("storePassword")
                keyAlias = releaseSigningProperty("keyAlias")
                keyPassword = releaseSigningProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            signingConfig = if (hasReleaseSigningConfig) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
            }
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

flutter {
    source = "../.."
}

dependencies {
    testImplementation(kotlin("test"))
}

tasks.register("checkReleaseSigningConfig") {
    group = "verification"
    description = "Checks whether Android release signing material is ready for external RC distribution."

    doLast {
        if (!hasReleaseSigningConfig) {
            throw GradleException(
                "Android release signing material is not ready:\n" +
                    releaseSigningIssues.joinToString(separator = "\n") { "- $it" }
            )
        }

        logger.lifecycle(
            "Android release signing material is ready: ${releaseSigningProperty("storeFile")}"
        )
    }
}
