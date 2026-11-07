export const PENSION_TYPES = [
  'Superannuation (Regular Retirement)', 'Compulsory Retirement', 'Invalid (Disability) Pension',
  'Family Pension', 'Compensation Pension'
]
export const BANKS = {
  'State-owned Commercial': ['Sonali Bank', 'Janata Bank', 'Agrani Bank', 'Rupali Bank'],
  'Private Commercial': ['Dutch-Bangla Bank', 'BRAC Bank', 'City Bank', 'Eastern Bank'],
  'Islamic': ['Islami Bank Bangladesh', 'Al-Arafah Islami Bank', 'Social Islami Bank'],
  'Specialized': ['Bangladesh Krishi Bank', 'Rajshahi Krishi Unnayan Bank'],
  'Mobile Financial Service': ['bKash', 'Nagad', 'Rocket']
}
export const MAX_YEAR = 2026
export const MIN_DATE = '1900-01-01'
export const MAX_DATE = `${MAX_YEAR}-12-31`
// Minimum age at retirement per pension type (demo values, keep in sync with Catalog.MinRetirementAge)
export const MIN_AGE = {
  'Superannuation (Regular Retirement)': 59, 'Compulsory Retirement': 50,
  'Invalid (Disability) Pension': 18, 'Family Pension': 0, 'Compensation Pension': 18
}
export const ageAt = (dob, on) => {
  const d = new Date(dob), r = new Date(on)
  let a = r.getFullYear() - d.getFullYear()
  if (r.getMonth() < d.getMonth() || (r.getMonth() === d.getMonth() && r.getDate() < d.getDate())) a--
  return a
}
export const GENDERS = ['Male', 'Female', 'Other']
export const RELATIONS = ['Spouse', 'Son', 'Daughter', 'Father', 'Mother', 'Brother', 'Sister', 'Other']
