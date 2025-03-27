/**
 * Stripe integration for FastyBox
 */

let stripeInstance = null;

// Initialize Stripe with the publishable key
async function initializeStripe() {
    if (stripeInstance) return stripeInstance;

    try {
        console.log("Fetching Stripe configuration...");
        const response = await fetch('/api/stripe/config');

        if (!response.ok) {
            console.error("Failed to fetch Stripe configuration:", response.statusText);
            throw new Error(`HTTP error! Status: ${response.status}`);
        }

        const { publishableKey } = await response.json();

        if (!publishableKey) {
            console.error("No publishable key returned from server");
            throw new Error("Missing publishable key");
        }

        console.log("Initializing Stripe with publishable key");
        stripeInstance = Stripe(publishableKey);
        return stripeInstance;
    } catch (error) {
        console.error("Error initializing Stripe:", error);
        alert("Error initializing payment processor. Please try again or contact support.");
        return null;
    }
}

// Create a checkout session and redirect to Stripe
async function createCheckoutSession(requestId, amount, paymentType = 0) {
    try {
        console.log(`Creating checkout session for request ${requestId} with amount ${amount}`);

        // Show loading indicator
        document.body.style.cursor = 'wait';

        // Create session on the server
        const response = await fetch('/api/stripe/create-checkout-session', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                requestId: requestId,
                amount: amount,
                paymentType: paymentType
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP error! Status: ${response.status}`);
        }

        const { sessionId } = await response.json();

        if (!sessionId) {
            throw new Error("No session ID returned from server");
        }

        console.log(`Session created: ${sessionId}`);
        return await redirectToStripeCheckout(sessionId);
    } catch (error) {
        console.error("Error creating checkout session:", error);
        alert("Error preparing payment. Please try again or contact support.");
        document.body.style.cursor = 'default';
        return false;
    }
}

// Redirect to Stripe Checkout
async function redirectToStripeCheckout(sessionId) {
    if (!sessionId) {
        console.error("Session ID not provided");
        return false;
    }

    console.log(`Redirecting to Stripe Checkout with session ID: ${sessionId}`);

    try {
        const stripe = await initializeStripe();
        if (!stripe) return false;

        const { error } = await stripe.redirectToCheckout({
            sessionId: sessionId
        });

        if (error) {
            console.error("Stripe redirect error:", error.message);
            alert("Error redirecting to payment form: " + error.message);
            document.body.style.cursor = 'default';
            return false;
        }

        return true;
    } catch (error) {
        console.error("Error redirecting to Stripe Checkout:", error);
        alert("Error processing payment. Please try again.");
        document.body.style.cursor = 'default';
        return false;
    }
}

// Export functions to global scope
window.initializeStripe = initializeStripe;
window.createCheckoutSession = createCheckoutSession;
window.redirectToStripeCheckout = redirectToStripeCheckout;