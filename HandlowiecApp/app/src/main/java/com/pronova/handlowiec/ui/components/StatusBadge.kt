package com.pronova.handlowiec.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.pronova.handlowiec.ui.theme.StatusCancelled
import com.pronova.handlowiec.ui.theme.StatusCompleted
import com.pronova.handlowiec.ui.theme.StatusInProgress
import com.pronova.handlowiec.ui.theme.StatusNew
import com.pronova.handlowiec.util.Constants

@Composable
fun StatusBadge(
    status: String,
    modifier: Modifier = Modifier
) {
    val (backgroundColor, textColor) = when (status) {
        Constants.STATUS_NEW -> StatusNew.copy(alpha = 0.15f) to StatusNew
        Constants.STATUS_COMPLETED -> StatusCompleted.copy(alpha = 0.15f) to StatusCompleted
        Constants.STATUS_CANCELLED -> StatusCancelled.copy(alpha = 0.15f) to StatusCancelled
        Constants.STATUS_IN_PROGRESS -> StatusInProgress.copy(alpha = 0.15f) to StatusInProgress
        else -> Color.Gray.copy(alpha = 0.15f) to Color.Gray
    }

    Box(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(backgroundColor)
            .padding(horizontal = 10.dp, vertical = 4.dp)
    ) {
        Text(
            text = status,
            color = textColor,
            fontSize = 12.sp,
            fontWeight = FontWeight.SemiBold,
            style = MaterialTheme.typography.labelMedium
        )
    }
}

@Composable
fun FoliaBadge(modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(Color(0xFF7B1FA2).copy(alpha = 0.15f))
            .padding(horizontal = 8.dp, vertical = 2.dp)
    ) {
        Text(
            text = "Folia",
            color = Color(0xFF7B1FA2),
            fontSize = 11.sp,
            fontWeight = FontWeight.Medium
        )
    }
}

@Composable
fun HallalBadge(modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(Color(0xFF00695C).copy(alpha = 0.15f))
            .padding(horizontal = 8.dp, vertical = 2.dp)
    ) {
        Text(
            text = "Hallal",
            color = Color(0xFF00695C),
            fontSize = 11.sp,
            fontWeight = FontWeight.Medium
        )
    }
}
