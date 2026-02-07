package com.pronova.handlowiec.ui.dashboard

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.google.accompanist.swiperefresh.SwipeRefresh
import com.google.accompanist.swiperefresh.rememberSwipeRefreshState
import com.pronova.handlowiec.ui.components.DatePickerButton
import com.pronova.handlowiec.ui.components.ErrorDisplay
import com.pronova.handlowiec.ui.components.LoadingIndicator
import com.pronova.handlowiec.ui.theme.*
import com.pronova.handlowiec.util.CurrencyUtils

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    viewModel: DashboardViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val selectedDate by viewModel.selectedDate.collectAsState()
    val isRefreshing by viewModel.isRefreshing.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Pulpit",
                        fontWeight = FontWeight.SemiBold
                    )
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = PrimaryGreen,
                    titleContentColor = Color.White
                ),
                actions = {
                    IconButton(onClick = { viewModel.refresh() }) {
                        Icon(
                            imageVector = Icons.Default.Refresh,
                            contentDescription = "Odswiez",
                            tint = Color.White
                        )
                    }
                }
            )
        }
    ) { paddingValues ->
        SwipeRefresh(
            state = rememberSwipeRefreshState(isRefreshing),
            onRefresh = { viewModel.refresh() },
            modifier = Modifier.padding(paddingValues)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
            ) {
                // Date selector
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 8.dp),
                    contentAlignment = Alignment.Center
                ) {
                    DatePickerButton(
                        selectedDate = selectedDate,
                        onDateSelected = { viewModel.onDateSelected(it) }
                    )
                }

                when (val state = uiState) {
                    is DashboardUiState.Loading -> {
                        LoadingIndicator(message = "Ladowanie danych...")
                    }
                    is DashboardUiState.Error -> {
                        ErrorDisplay(
                            message = state.message,
                            onRetry = { viewModel.loadDashboard() }
                        )
                    }
                    is DashboardUiState.Success -> {
                        val data = state.data

                        // Stats cards grid
                        Column(
                            modifier = Modifier.padding(horizontal = 16.dp),
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            // Row 1: Total orders and order count
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(12.dp)
                            ) {
                                DashboardStatCard(
                                    title = "Suma zamowien",
                                    value = CurrencyUtils.formatKg(data.sumaZamowien),
                                    icon = Icons.Default.Scale,
                                    iconColor = PrimaryGreen,
                                    backgroundColor = PrimaryGreenContainer,
                                    modifier = Modifier.weight(1f)
                                )
                                DashboardStatCard(
                                    title = "Liczba zamowien",
                                    value = "${data.liczbaZamowien}",
                                    icon = Icons.Default.ShoppingCart,
                                    iconColor = StatusCompleted,
                                    backgroundColor = Color(0xFFBBDEFB),
                                    modifier = Modifier.weight(1f)
                                )
                            }

                            // Row 2: Customers and pallets
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(12.dp)
                            ) {
                                DashboardStatCard(
                                    title = "Liczba klientow",
                                    value = "${data.liczbaKlientow}",
                                    icon = Icons.Default.People,
                                    iconColor = SecondaryAmber,
                                    backgroundColor = SecondaryAmberContainer,
                                    modifier = Modifier.weight(1f)
                                )
                                DashboardStatCard(
                                    title = "Liczba palet",
                                    value = "${data.sumaPalet}",
                                    icon = Icons.Default.Inventory2,
                                    iconColor = Color(0xFF6A1B9A),
                                    backgroundColor = Color(0xFFE1BEE7),
                                    modifier = Modifier.weight(1f)
                                )
                            }

                            // Row 3: Cancelled
                            DashboardStatCard(
                                title = "Anulowane",
                                value = "${data.liczbaAnulowanych}",
                                icon = Icons.Default.Cancel,
                                iconColor = StatusCancelled,
                                backgroundColor = ErrorContainer,
                                modifier = Modifier.fillMaxWidth()
                            )
                        }

                        Spacer(modifier = Modifier.height(24.dp))
                    }
                }
            }
        }
    }
}

@Composable
fun DashboardStatCard(
    title: String,
    value: String,
    icon: ImageVector,
    iconColor: Color,
    backgroundColor: Color,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.weight(1f)
                )
                Box(
                    modifier = Modifier
                        .size(36.dp)
                        .padding(4.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Surface(
                        shape = RoundedCornerShape(8.dp),
                        color = backgroundColor,
                        modifier = Modifier.fillMaxSize()
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = icon,
                                contentDescription = null,
                                tint = iconColor,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    }
                }
            }
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = value,
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSurface
            )
        }
    }
}
