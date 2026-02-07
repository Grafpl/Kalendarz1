package com.pronova.handlowiec.ui.navigation

import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.pronova.handlowiec.ui.auth.LoginScreen
import com.pronova.handlowiec.ui.customers.CustomerDetailScreen
import com.pronova.handlowiec.ui.customers.CustomersListScreen
import com.pronova.handlowiec.ui.dashboard.DashboardScreen
import com.pronova.handlowiec.ui.orders.OrderCreateScreen
import com.pronova.handlowiec.ui.orders.OrderDetailScreen
import com.pronova.handlowiec.ui.orders.OrdersListScreen
import com.pronova.handlowiec.ui.products.ProductsListScreen
import com.pronova.handlowiec.ui.theme.PrimaryGreen

// Route constants
object Routes {
    const val LOGIN = "login"
    const val DASHBOARD = "dashboard"
    const val ORDERS = "orders"
    const val ORDER_DETAIL = "orderDetail/{orderId}"
    const val ORDER_CREATE = "orderCreate"
    const val CUSTOMERS = "customers"
    const val CUSTOMER_DETAIL = "customerDetail/{customerId}"
    const val PRODUCTS = "products"

    fun orderDetail(id: Int) = "orderDetail/$id"
    fun customerDetail(id: Int) = "customerDetail/$id"
}

// Bottom navigation items
sealed class BottomNavItem(
    val route: String,
    val title: String,
    val selectedIcon: ImageVector,
    val unselectedIcon: ImageVector
) {
    data object Dashboard : BottomNavItem(
        route = Routes.DASHBOARD,
        title = "Pulpit",
        selectedIcon = Icons.Filled.Dashboard,
        unselectedIcon = Icons.Outlined.Dashboard
    )

    data object Orders : BottomNavItem(
        route = Routes.ORDERS,
        title = "Zamowienia",
        selectedIcon = Icons.Filled.ShoppingCart,
        unselectedIcon = Icons.Outlined.ShoppingCart
    )

    data object Customers : BottomNavItem(
        route = Routes.CUSTOMERS,
        title = "Klienci",
        selectedIcon = Icons.Filled.People,
        unselectedIcon = Icons.Outlined.People
    )

    data object Products : BottomNavItem(
        route = Routes.PRODUCTS,
        title = "Produkty",
        selectedIcon = Icons.Filled.Inventory,
        unselectedIcon = Icons.Outlined.Inventory
    )
}

val bottomNavItems = listOf(
    BottomNavItem.Dashboard,
    BottomNavItem.Orders,
    BottomNavItem.Customers,
    BottomNavItem.Products
)

// Routes where bottom nav should be shown
val bottomNavRoutes = listOf(
    Routes.DASHBOARD,
    Routes.ORDERS,
    Routes.CUSTOMERS,
    Routes.PRODUCTS
)

@Composable
fun AppNavigation() {
    val navController = rememberNavController()
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route

    // Determine if bottom nav should be visible
    val showBottomNav = currentRoute in bottomNavRoutes

    Scaffold(
        bottomBar = {
            if (showBottomNav) {
                BottomNavigationBar(
                    navController = navController,
                    currentRoute = currentRoute
                )
            }
        }
    ) { paddingValues ->
        NavHost(
            navController = navController,
            startDestination = Routes.LOGIN,
            modifier = if (showBottomNav) Modifier.padding(paddingValues) else Modifier
        ) {
            // Login
            composable(Routes.LOGIN) {
                LoginScreen(
                    onLoginSuccess = {
                        navController.navigate(Routes.DASHBOARD) {
                            popUpTo(Routes.LOGIN) { inclusive = true }
                        }
                    }
                )
            }

            // Dashboard
            composable(Routes.DASHBOARD) {
                DashboardScreen()
            }

            // Orders List
            composable(Routes.ORDERS) {
                OrdersListScreen(
                    onOrderClick = { orderId ->
                        navController.navigate(Routes.orderDetail(orderId))
                    },
                    onCreateOrder = {
                        navController.navigate(Routes.ORDER_CREATE)
                    }
                )
            }

            // Order Detail
            composable(
                route = Routes.ORDER_DETAIL,
                arguments = listOf(navArgument("orderId") { type = NavType.IntType })
            ) {
                OrderDetailScreen(
                    onNavigateBack = { navController.popBackStack() },
                    onEdit = { orderId ->
                        // For now navigate to create screen - could be edit in future
                        navController.navigate(Routes.ORDER_CREATE)
                    }
                )
            }

            // Order Create
            composable(Routes.ORDER_CREATE) {
                OrderCreateScreen(
                    onNavigateBack = { navController.popBackStack() },
                    onOrderCreated = {
                        navController.popBackStack()
                    }
                )
            }

            // Customers List
            composable(Routes.CUSTOMERS) {
                CustomersListScreen(
                    onCustomerClick = { customerId ->
                        navController.navigate(Routes.customerDetail(customerId))
                    }
                )
            }

            // Customer Detail
            composable(
                route = Routes.CUSTOMER_DETAIL,
                arguments = listOf(navArgument("customerId") { type = NavType.IntType })
            ) {
                CustomerDetailScreen(
                    onNavigateBack = { navController.popBackStack() },
                    onCreateOrder = { customerId ->
                        navController.navigate(Routes.ORDER_CREATE)
                    }
                )
            }

            // Products List
            composable(Routes.PRODUCTS) {
                ProductsListScreen()
            }
        }
    }
}

@Composable
fun BottomNavigationBar(
    navController: NavHostController,
    currentRoute: String?
) {
    NavigationBar(
        containerColor = Color.White,
        tonalElevation = 8.dp
    ) {
        bottomNavItems.forEach { item ->
            val isSelected = currentRoute == item.route

            NavigationBarItem(
                selected = isSelected,
                onClick = {
                    if (currentRoute != item.route) {
                        navController.navigate(item.route) {
                            popUpTo(navController.graph.findStartDestination().id) {
                                saveState = true
                            }
                            launchSingleTop = true
                            restoreState = true
                        }
                    }
                },
                icon = {
                    Icon(
                        imageVector = if (isSelected) item.selectedIcon else item.unselectedIcon,
                        contentDescription = item.title
                    )
                },
                label = {
                    Text(
                        text = item.title,
                        fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal
                    )
                },
                colors = NavigationBarItemDefaults.colors(
                    selectedIconColor = PrimaryGreen,
                    selectedTextColor = PrimaryGreen,
                    unselectedIconColor = Color.Gray,
                    unselectedTextColor = Color.Gray,
                    indicatorColor = PrimaryGreen.copy(alpha = 0.12f)
                )
            )
        }
    }
}
