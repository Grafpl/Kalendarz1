package com.pronova.handlowiec.data.model

import com.google.gson.annotations.SerializedName

data class LoginResponse(
    @SerializedName("token")
    val token: String,

    @SerializedName("userName")
    val userName: String,

    @SerializedName("fullName")
    val fullName: String,

    @SerializedName("handlowiecName")
    val handlowiecName: String
)
