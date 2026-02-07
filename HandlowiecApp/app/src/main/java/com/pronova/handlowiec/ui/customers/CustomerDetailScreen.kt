package com.pronova.handlowiec.ui.customers

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
import com.pronova.handlowiec.data.model.Customer
import com.pronova.handlowiec.ui.components.ErrorDisplay
import com.pronova.handlowiec.ui.components.LoadingIndicator
import com.pronova.handlowiec.ui.theme.*
import com.pronova.handlowiec.util.CurrencyUtils

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CustomerDetailScreen(
    onNavigateBack: () -> Unit,
    onCreateOrder: (Int) -> Unit,
    viewModel: CustomerDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        "Szczegoly klienta",
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
            is CustomerDetailUiState.Loading -> {
                LoadingIndicator(
                    message = "Ladowanie danych klienta...",
                    modifier = Modifier.padding(paddingValues)
                )
            }
            is CustomerDetailUiState.Error -> {
                ErrorDisplay(
                    message = state.message,
                    onRetry = { viewModel.loadCustomer() },
                    modifier = Modifier.padding(paddingValues)
                )
            }
            is CustomerDetailUiState.Success -> {
                val customer = state.customer

                Column(
                    modifier = Modifier
                        .padding(paddingValues)
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                ) {
                    // Header card
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
                                Surface(
                                    shape = RoundedCornerShape(8.dp),
                                    color = PrimaryGreen.copy(alpha = 0.15f)
                                ) {
                                    Text(
                                        text = customer.shortcut,
                                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                                        style = MaterialTheme.typography.titleMedium,
                                        fontWeight = FontWeight.Bold,
                                        color = PrimaryGreen
                                    )
                                }
                                Surface(
                                    shape = RoundedCornerShape(8.dp),
                                    color = if (customer.aktywny) PrimaryGreen.copy(alpha = 0.15f)
                                    else StatusCancelled.copy(alpha = 0.15f)
                                ) {
                                    Text(
                                        text = if (customer.aktywny) "Aktywny" else "Nieaktywny",
                                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                                        style = MaterialTheme.typography.labelMedium,
                                        fontWeight = FontWeight.SemiBold,
                                        color = if (customer.aktywny) PrimaryGreen else StatusCancelled
                                    )
                                }
                            }

                            Spacer(modifier = Modifier.height(12.dp))

                            Text(
                                text = customer.nazwa,
                                style = MaterialTheme.typography.headlineSmall,
                                fontWeight = FontWeight.Bold
                            )

                            if (customer.handlowiec.isNotBlank()) {
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    text = "Handlowiec: ${customer.handlowiec}",
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }

                    // Contact info card
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
                                text = "Dane kontaktowe",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(12.dp))

                            if (customer.nip.isNotBlank()) {
                                CustomerInfoRow(
                                    icon = Icons.Default.Badge,
                                    label = "NIP",
                                    value = customer.nip
                                )
                            }
                            if (customer.adres.isNotBlank()) {
                                CustomerInfoRow(
                                    icon = Icons.Default.Home,
                                    label = "Adres",
                                    value = customer.adres
                                )
                            }
                            if (customer.miasto.isNotBlank() || customer.kodPocztowy.isNotBlank()) {
                                CustomerInfoRow(
                                    icon = Icons.Default.LocationCity,
                                    label = "Miasto",
                                    value = "${customer.kodPocztowy} ${customer.miasto}".trim()
                                )
                            }
                            if (customer.telefon.isNotBlank()) {
                                CustomerInfoRow(
                                    icon = Icons.Default.Phone,
                                    label = "Telefon",
                                    value = customer.telefon
                                )
                            }
                            if (customer.email.isNotBlank()) {
                                CustomerInfoRow(
                                    icon = Icons.Default.Email,
                                    label = "Email",
                                    value = customer.email
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(12.dp))

                    // Financial info card
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
                                text = "Informacje finansowe",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(12.dp))

                            // Payment terms
                            CustomerInfoRow(
                                icon = Icons.Default.Schedule,
                                label = "Termin platnosci",
                                value = "${customer.terminPlatnosci} dni"
                            )

                            // Credit limit
                            CustomerInfoRow(
                                icon = Icons.Default.AccountBalance,
                                label = "Limit kredytowy",
                                value = CurrencyUtils.formatPLN(customer.limitKredytowy)
                            )

                            // Balance
                            CustomerInfoRow(
                                icon = Icons.Default.Payment,
                                label = "Saldo naleznosci",
                                value = CurrencyUtils.formatPLN(customer.saldoNaleznosci)
                            )

                            Spacer(modifier = Modifier.height(16.dp))

                            // Credit usage bar
                            val creditUsage = CurrencyUtils.calculateCreditUsage(
                                customer.saldoNaleznosci,
                                customer.limitKredytowy
                            )

                            Text(
                                text = "Wykorzystanie kredytu",
                                style = MaterialTheme.typography.titleSmall,
                                fontWeight = FontWeight.Medium
                            )
                            Spacer(modifier = Modifier.height(8.dp))

                            LinearProgressIndicator(
                                progress = { (creditUsage / 100f).toFloat() },
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(12.dp),
                                color = when {
                                    creditUsage > 90 -> StatusCancelled
                                    creditUsage > 70 -> StatusInProgress
                                    else -> PrimaryGreen
                                },
                                trackColor = MaterialTheme.colorScheme.surfaceVariant,
                                strokeCap = androidx.compose.ui.graphics.StrokeCap.Round
                            )

                            Spacer(modifier = Modifier.height(4.dp))

                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    text = CurrencyUtils.formatPLN(customer.saldoNaleznosci),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                Text(
                                    text = "${CurrencyUtils.formatPrice(creditUsage)}%",
                                    style = MaterialTheme.typography.bodySmall,
                                    fontWeight = FontWeight.SemiBold,
                                    color = when {
                                        creditUsage > 90 -> StatusCancelled
                                        creditUsage > 70 -> StatusInProgress
                                        else -> PrimaryGreen
                                    }
                                )
                                Text(
                                    text = CurrencyUtils.formatPLN(customer.limitKredytowy),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    // Create order button
                    Button(
                        onClick = { onCreateOrder(customer.id) },
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp)
                            .height(52.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = PrimaryGreen)
                    ) {
                        Icon(
                            Icons.Default.ShoppingCart,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            "Zloz zamowienie",
                            fontWeight = FontWeight.SemiBold
                        )
                    }

                    Spacer(modifier = Modifier.height(24.dp))
                }
            }
        }
    }
}

@Composable
fun CustomerInfoRow(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = PrimaryGreen,
            modifier = Modifier.size(20.dp)
        )
        Spacer(modifier = Modifier.width(12.dp))
        Column {
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(
                text = value,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium
            )
        }
    }
}
