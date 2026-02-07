package com.pronova.handlowiec.ui.orders

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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.pronova.handlowiec.data.model.Order
import com.pronova.handlowiec.ui.components.*
import com.pronova.handlowiec.ui.theme.*
import com.pronova.handlowiec.util.Constants
import com.pronova.handlowiec.util.CurrencyUtils
import com.pronova.handlowiec.util.DateUtils

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrderDetailScreen(
    onNavigateBack: () -> Unit,
    onEdit: (Int) -> Unit,
    viewModel: OrderDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val cancelState by viewModel.cancelState.collectAsState()
    var showCancelDialog by remember { mutableStateOf(false) }

    // Handle cancel success
    LaunchedEffect(cancelState) {
        if (cancelState is CancelOrderState.Success) {
            viewModel.resetCancelState()
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Szczegoly zamowienia",
                        fontWeight = FontWeight.SemiBold
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.Default.ArrowBack,
                            contentDescription = "Wstecz",
                            tint = Color.White
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = PrimaryGreen,
                    titleContentColor = Color.White
                )
            )
        }
    ) { paddingValues ->
        when (val state = uiState) {
            is OrderDetailUiState.Loading -> {
                LoadingIndicator(
                    message = "Ladowanie zamowienia...",
                    modifier = Modifier.padding(paddingValues)
                )
            }
            is OrderDetailUiState.Error -> {
                ErrorDisplay(
                    message = state.message,
                    onRetry = { viewModel.loadOrder() },
                    modifier = Modifier.padding(paddingValues)
                )
            }
            is OrderDetailUiState.Success -> {
                val order = state.order

                Column(
                    modifier = Modifier
                        .padding(paddingValues)
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                ) {
                    // Status and ID header
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White),
                        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(
                                    text = "Zamowienie #${order.id}",
                                    style = MaterialTheme.typography.titleLarge,
                                    fontWeight = FontWeight.Bold
                                )
                                StatusBadge(status = order.status)
                            }

                            Spacer(modifier = Modifier.height(12.dp))

                            // Customer info
                            DetailRow("Odbiorca", order.odbiorca)
                            DetailRow("Handlowiec", order.handlowiec)
                            DetailRow("Waluta", order.waluta)
                        }
                    }

                    // Amounts card
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White),
                        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Text(
                                text = "Ilosci",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(12.dp))

                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceEvenly
                            ) {
                                AmountColumn(
                                    label = "Zamowione",
                                    value = CurrencyUtils.formatKg(order.iloscZamowiona),
                                    color = PrimaryGreen
                                )
                                AmountColumn(
                                    label = "Faktyczne",
                                    value = CurrencyUtils.formatKg(order.iloscFaktyczna),
                                    color = StatusCompleted
                                )
                                AmountColumn(
                                    label = "Roznica",
                                    value = CurrencyUtils.formatKg(order.roznica),
                                    color = if (order.roznica < 0) StatusCancelled else PrimaryGreen
                                )
                            }

                            Spacer(modifier = Modifier.height(12.dp))
                            HorizontalDivider(color = Divider)
                            Spacer(modifier = Modifier.height(12.dp))

                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceEvenly
                            ) {
                                AmountColumn(
                                    label = "Pojemniki",
                                    value = "${order.pojemniki}",
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                                AmountColumn(
                                    label = "Palety",
                                    value = "${order.palety}",
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                                AmountColumn(
                                    label = "Sr. cena",
                                    value = CurrencyUtils.formatPrice(order.sredniaCena),
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(12.dp))

                    // Dates and details card
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White),
                        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Text(
                                text = "Terminy",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(8.dp))
                            DetailRow("Data przyjecia", DateUtils.formatForDisplay(order.dataPrzyjecia))
                            DetailRow("Godzina przyjecia", DateUtils.formatTimeForDisplay(order.godzinaPrzyjecia))
                            DetailRow("Data uboju", DateUtils.formatForDisplay(order.dataUboju))

                            if (order.trybE2) {
                                Spacer(modifier = Modifier.height(8.dp))
                                StatusBadge(status = "Tryb E2")
                            }

                            // Badges
                            if (order.maFolie || order.maHallal || order.czyMaCeny) {
                                Spacer(modifier = Modifier.height(8.dp))
                                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                    if (order.maFolie) FoliaBadge()
                                    if (order.maHallal) HallalBadge()
                                }
                            }

                            // Notes
                            if (order.uwagi.isNotBlank()) {
                                Spacer(modifier = Modifier.height(12.dp))
                                HorizontalDivider(color = Divider)
                                Spacer(modifier = Modifier.height(12.dp))
                                Text(
                                    text = "Uwagi",
                                    style = MaterialTheme.typography.titleSmall,
                                    fontWeight = FontWeight.SemiBold
                                )
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    text = order.uwagi,
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(12.dp))

                    // Order items
                    if (order.pozycje.isNotEmpty()) {
                        Text(
                            text = "Pozycje zamowienia (${order.pozycje.size})",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.SemiBold,
                            modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
                        )

                        order.pozycje.forEach { item ->
                            OrderItemRow(item = item)
                        }
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    // Action buttons
                    if (order.status != Constants.STATUS_CANCELLED && !order.czyZrealizowane) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(horizontal = 16.dp),
                            horizontalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            OutlinedButton(
                                onClick = { showCancelDialog = true },
                                modifier = Modifier.weight(1f),
                                colors = ButtonDefaults.outlinedButtonColors(
                                    contentColor = StatusCancelled
                                ),
                                enabled = cancelState !is CancelOrderState.Loading
                            ) {
                                Icon(
                                    Icons.Default.Cancel,
                                    contentDescription = null,
                                    modifier = Modifier.size(18.dp)
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text("Anuluj")
                            }

                            Button(
                                onClick = { onEdit(order.id) },
                                modifier = Modifier.weight(1f),
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = PrimaryGreen
                                )
                            ) {
                                Icon(
                                    Icons.Default.Edit,
                                    contentDescription = null,
                                    modifier = Modifier.size(18.dp)
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text("Edytuj")
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(24.dp))
                }
            }
        }
    }

    // Cancel confirmation dialog
    if (showCancelDialog) {
        AlertDialog(
            onDismissRequest = { showCancelDialog = false },
            title = { Text("Anuluj zamowienie") },
            text = { Text("Czy na pewno chcesz anulowac to zamowienie?") },
            confirmButton = {
                TextButton(
                    onClick = {
                        showCancelDialog = false
                        viewModel.cancelOrder()
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = StatusCancelled)
                ) {
                    Text("Tak, anuluj")
                }
            },
            dismissButton = {
                TextButton(onClick = { showCancelDialog = false }) {
                    Text("Nie")
                }
            }
        )
    }
}

@Composable
fun DetailRow(
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

@Composable
fun AmountColumn(
    label: String,
    value: String,
    color: Color,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = value,
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.Bold,
            color = color
        )
    }
}
