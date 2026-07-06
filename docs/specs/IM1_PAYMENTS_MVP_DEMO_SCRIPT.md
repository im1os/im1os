# iM1 Payments MVP Demo Script

Purpose: prove the complete iM1 Payments MVP flow from company merchant application through NMI sandbox merchant activation and first card payment.

## Prerequisites

- Dev environment has the NMI sandbox partner credentials configured in `.env`.
- Database migrations are applied.
- You can sign in as both a company user and a platform staff user.
- The company workspace has an organization context.
- Use NMI sandbox test card data for the hosted fields payment step.

## Script

1. Create a new Company.
   - Expected: the company appears in the tenant/company list and can be opened as a company workspace.

2. Open Company > Financial Services.
   - Expected: Merchant Account shows `Not Started` or no active processing account, with payments disabled.

3. Open Financial Services > Merchant Account and complete the merchant application.
   - Enter Business Name, DBA, EIN or Tax ID, Business Type, Physical Address, Mailing Address, Owner Name, Owner Email, Owner Phone, Banking Information, Expected Monthly Volume, Average Ticket, Website, and MCC if available.
   - Expected: Save Draft keeps the application in `Draft` without creating an NMI merchant.

4. Submit the merchant application.
   - Expected: company-facing Merchant Account status becomes `Submitted`.
   - Expected: payments remain disabled.

5. Log into the Platform workspace.
   - Expected: platform navigation is available.

6. Open Platform > Financial Services > Merchant Applications and approve the application.
   - Expected: the submitted company appears in the grid with Company, Status, Submitted Date, Business Name, Owner, Expected Volume, Provider Merchant ID, credential flags, and actions.
   - Click Approve.
   - Expected: iM1 calls the NMI Partner API to create the merchant and create merchant API keys automatically.

7. Verify the merchant exists in the NMI sandbox.
   - Expected: NMI sandbox has the created merchant/gateway account.
   - Expected: iM1 has persisted the provider merchant ID, gateway username, payment API key, and tokenization public key in the provider relationship record.

8. Return to the Company workspace.
   - Expected: Company > Financial Services > Merchant Account shows status `Active`.
   - Expected: Provider is `NMI`, merchant relationship is active, payments enabled is `Yes`, and processing readiness is enabled.

9. Confirm payment configuration.
   - Open Company > Payments, or Company > Financial Services > Merchant Account > Open Test Payment.
   - Expected: the page shows the NMI sandbox configuration as active.
   - Expected: hosted card fields load using the merchant tokenization key.
   - Expected: no manual credential entry is required.

10. Process a $1.00 sandbox payment.
    - Enter amount `1.00`, card holder details, and NMI sandbox card details in the hosted fields.
    - Click Submit Payment.
    - Expected: result displays Approved or Declined, with authorization code, transaction ID, and timestamp when returned by NMI.

11. Verify Transaction History.
    - Expected: the payment appears in Recent Transactions / Payment History for the company.
    - Expected: the transaction is persisted with provider `NMI`, amount `$1.00`, status, gateway transaction ID, authorization code, and timestamp.

## Success Criteria

- Company completes merchant application.
- Platform receives submitted application.
- Platform approves the application.
- NMI creates the merchant successfully.
- Merchant credentials are saved automatically.
- Merchant account becomes `Active`.
- Payments are enabled without manual credential entry.
- PaymentService recognizes the active merchant.
- Company processes a sandbox payment.
- Transaction is saved.
- Transaction appears in history.
